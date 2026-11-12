#!/usr/bin/env bash
set -euo pipefail

repo="$(realpath -- "$(dirname -- "${BASH_SOURCE[0]}")/..")"
[[ -d "${repo}/artifacts" ]] || { printf 'Repository artifacts directory missing.\n' >&2; exit 1; }
root="$(mktemp -d "${repo}/artifacts/installer-wsl-test.XXXXXX")"
trap 'rm -rf -- "$root"' EXIT

export HOME="${root}/home"
export PFW_TEST_ROOT="$root"
mkdir -p "${HOME}/.local/bin" "${HOME}/.config/PinentryForWindows/relay" \
  "${root}/fake-bin" "${root}/gnupg"
export PATH="${root}/fake-bin:${PATH}"

cat > "${root}/fake-bin/gpgconf" <<'EOF'
#!/usr/bin/env bash
if [[ "$1" == '--list-dirs' && "$2" == 'homedir' ]]; then
  printf '%s\n' "${PFW_TEST_ROOT}/gnupg"
elif [[ "$1" == '--kill' && "$2" == 'gpg-agent' ]]; then
  exit 0
else
  exit 2
fi
EOF
chmod 755 "${root}/fake-bin/gpgconf"

cat > "${root}/relay-stub" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  --init)
    printf 'disposable private key\n' > "$2/client.pfx"
    printf 'disposable public certificate\n' > "$2/client.cer" ;;
  --renew)
    printf '%s %s\n' "$1" "$2" >> "${PFW_TEST_ROOT}/calls"
    printf 'renewed private key\n' > "$2/client.pfx"
    printf 'renewed public certificate\n' > "$2/client.cer" ;;
  --trust-broker)
    if [[ -f "$2/broker.cer" && "${4:-}" != '--replace' ]] && ! cmp -s -- "$3" "$2/broker.cer"; then
      exit 3
    fi
    cp -- "$3" "$2/broker.cer" ;;
  --remove-identity)
    printf '%s %s\n' "$1" "$2" >> "${PFW_TEST_ROOT}/calls"
    rm -f -- "$2/client.pfx" "$2/client.cer" "$2/broker.cer" ;;
  *) exit 2 ;;
esac
EOF

case "$(uname -m)" in
  x86_64) asset='pfw-relay-x64' ;;
  aarch64) asset='pfw-relay-arm64' ;;
  *) printf 'Unsupported test architecture.\n' >&2; exit 1 ;;
esac
installed="${HOME}/.local/bin/${asset}"
config_dir="${HOME}/.config/PinentryForWindows/relay"
install -m 755 "${root}/relay-stub" "$installed"
printf 'disposable private key\n' > "${config_dir}/client.pfx"
printf 'disposable public certificate\n' > "${config_dir}/client.cer"
printf 'disposable broker pin\n' > "${config_dir}/broker.cer"
printf 'unrelated setting\n' > "${config_dir}/keep.txt"
printf 'default-cache-ttl 300\npinentry-program %s\n' "$installed" > "${root}/gnupg/gpg-agent.conf"

if cat "${repo}/tools/setup-wsl.sh" | bash -s -- --revoked-on-windows > "${root}/invalid-output" 2>&1; then
  printf 'Revocation flag was accepted without uninstall.\n' >&2
  exit 1
fi
[[ -f "${config_dir}/client.pfx" ]] || { printf 'Invalid invocation altered private key.\n' >&2; exit 1; }

cat "${repo}/tools/setup-wsl.sh" | bash -s -- --uninstall > "${root}/first-output"
if ! grep -qi 'revoke' "${root}/first-output"; then
  printf 'Uninstall did not explain Windows revocation before identity removal.\n' >&2
  exit 1
fi
grep -Fq 'https://exec.baliestri.dev/pfw-setup-wsl.sh' "${root}/first-output"
[[ ! -e "$installed" && -f "${config_dir}/client.pfx" && -f "${config_dir}/client.cer" ]]
grep -q '^default-cache-ttl 300$' "${root}/gnupg/gpg-agent.conf"
if grep -q '^pinentry-program ' "${root}/gnupg/gpg-agent.conf"; then
  printf 'Uninstall kept its GnuPG setting.\n' >&2
  exit 1
fi

install -m 755 "${root}/relay-stub" "$installed"
bash "${repo}/tools/setup-wsl.sh" --uninstall --revoked-on-windows > "${root}/second-output"
[[ ! -e "$installed" && ! -e "${config_dir}/client.pfx" &&
   ! -e "${config_dir}/client.cer" && ! -e "${config_dir}/broker.cer" ]]
[[ -f "${config_dir}/keep.txt" ]]
grep -Fq -- "--remove-identity ${config_dir}" "${root}/calls"

printf 'disposable private key\n' > "${config_dir}/client.pfx"
printf 'disposable public certificate\n' > "${config_dir}/client.cer"
bash "${repo}/tools/setup-wsl.sh" --uninstall --revoked-on-windows > "${root}/fallback-output"
[[ ! -e "${config_dir}/client.pfx" && ! -e "${config_dir}/client.cer" ]]
[[ -f "${config_dir}/keep.txt" ]]

cat > "${root}/fake-bin/curl" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
destination=''
while (($#)); do
  case "$1" in
    --output) destination="$2"; shift 2 ;;
    *) shift ;;
  esac
done
case "${destination##*/}" in
  "$PFW_TEST_ASSET") cp -- "${PFW_TEST_ROOT}/relay-stub" "$destination" ;;
  SHA256SUMS.txt)
    printf '%s  %s\n' "$(sha256sum "${PFW_TEST_ROOT}/relay-stub" | cut -d ' ' -f1)" \
      "$PFW_TEST_ASSET" > "$destination" ;;
  *) exit 2 ;;
esac
EOF
chmod 755 "${root}/fake-bin/curl"
export PFW_TEST_ASSET="$asset"
cat "${repo}/tools/setup-wsl.sh" | bash > "${root}/pipe-install-output"
[[ -x "$installed" && -s "${config_dir}/client.pfx" && -s "${config_dir}/client.cer" ]]
grep -Fq 'https://exec.baliestri.dev/pfw-setup-bridge.ps1' "${root}/pipe-install-output"
grep -Fq 'https://exec.baliestri.dev/pfw-setup-wsl.sh' "${root}/pipe-install-output"

printf 'disposable broker certificate\n' > "${root}/broker.cer"
cat "${repo}/tools/setup-wsl.sh" | bash -s -- \
  --broker-certificate "${root}/broker.cer" --activate > "${root}/pipe-activate-output"
grep -Fq "pinentry-program ${installed}" "${root}/gnupg/gpg-agent.conf"
[[ -s "${config_dir}/broker.cer" ]]

if bash "${repo}/tools/setup-wsl.sh" --replace-broker > "${root}/replace-invalid-output" 2>&1; then
  printf 'Broker replacement was accepted without a certificate.\n' >&2
  exit 1
fi

printf 'renewed broker certificate\n' > "${root}/renewed-broker.cer"
if bash "${repo}/tools/setup-wsl.sh" --broker-certificate "${root}/renewed-broker.cer" \
     > "${root}/repin-output" 2>&1; then
  printf 'A different broker certificate replaced the pin without --replace-broker.\n' >&2
  exit 1
fi
grep -Fxq 'disposable broker certificate' "${config_dir}/broker.cer"

bash "${repo}/tools/setup-wsl.sh" --renew --broker-certificate "${root}/renewed-broker.cer" \
  --replace-broker > "${root}/renew-output"
grep -Fq -- "--renew ${config_dir}" "${root}/calls"
grep -Fxq 'renewed public certificate' "${config_dir}/client.cer"
grep -Fxq 'renewed broker certificate' "${config_dir}/broker.cer"
grep -Fq "pinentry-program ${installed}" "${root}/gnupg/gpg-agent.conf"
printf 'WSL streamed setup, activation, renewal, and revocation cleanup: PASS\n'
