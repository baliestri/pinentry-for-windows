# Pinentry For Windows Changelog

## [2.0.1](https://github.com/baliestri/pinentry-for-windows/compare/v2.0.0..v2.0.1) - 2026-10-21

### 🐛 Bug Fixes

- *(ci)* Build release artifacts from the validated tag - ([b3e0b4a](https://github.com/baliestri/pinentry-for-windows/commit/b3e0b4a139c2ca28ab1ea5965e5c8f18c5937711))

### ⚙️ Miscellaneous Tasks

- *(build)* Remove fixed  version prefix - ([27e8843](https://github.com/baliestri/pinentry-for-windows/commit/27e8843dbd12298d3d770688b818e01edce6735f))

## New Contributors ❤️

* @EDM115 made their first contribution
## [2.0.0](https://github.com/baliestri/pinentry-for-windows/compare/1.2.1..v2.0.0) - 2026-10-11

### ⛰️  Features

- *(bridge)* Discover installed executable for streamed setup - ([2fbf0fe](https://github.com/baliestri/pinentry-for-windows/commit/2fbf0fec41fbdf2a3bd3f919a6ae162044a860c9))
- *(install)* Add safe Windows and WSL setup - ([79a477d](https://github.com/baliestri/pinentry-for-windows/commit/79a477dcd74d673eaa02109f16c4c83e100297c6))
- *(logging)* Add opt-in verbose diagnostics on both platforms - ([541f727](https://github.com/baliestri/pinentry-for-windows/commit/541f7278267b42aab154a64102a6b2e997106cb9))
- *(metrics)* Record prompt latency and helper resource usage - ([61c817a](https://github.com/baliestri/pinentry-for-windows/commit/61c817aea10fbc834a35552b24e511623a012802))
- *(pinentry)* [**breaking**] Replace legacy server with Windows and WSL bridge - ([74f4887](https://github.com/baliestri/pinentry-for-windows/commit/74f4887d1b5e0e540e8dbff87807c6a6bd7d3153))
- *(wsl)* Require revocation before deleting relay identity - ([a38ea0f](https://github.com/baliestri/pinentry-for-windows/commit/a38ea0fc8e6e8a07939df9e551be4ab2f930dacf))

### 🐛 Bug Fixes

- *(cache)* Clean up PRF data and native results on errors - ([fbed077](https://github.com/baliestri/pinentry-for-windows/commit/fbed077a30b796c274717ab4264d59cfb7384394))
- *(changelog)* Recognize unprefixed release tags - ([87b88be](https://github.com/baliestri/pinentry-for-windows/commit/87b88be07f42eb213f03a0a8b9445ef57d6a16b2))
- *(cli)* Avoid echoing unrecognized arguments - ([05130d8](https://github.com/baliestri/pinentry-for-windows/commit/05130d8c3643be9651236b928beabf93ce6c29e3))
- *(install)* Clear cache and broker on Windows uninstall - ([c7eb23d](https://github.com/baliestri/pinentry-for-windows/commit/c7eb23d263934a71d685135d6cdcb8efdcbe6202))
- *(relay)* Reject closure before Assuan greeting - ([f73daef](https://github.com/baliestri/pinentry-for-windows/commit/f73daefd7747c3f5f847cf781846904b423c4f38))
- *(release)* Extract typos from packaged archive path - ([adc7075](https://github.com/baliestri/pinentry-for-windows/commit/adc7075c9d1590f0850dffbdc52360236b207d0f))
- *(test)* Use native installer asset in offline fixture - ([c21b767](https://github.com/baliestri/pinentry-for-windows/commit/c21b7674619cf7c0bedb1618b477ed0937f33358))
- *(ui)* Wipe serialized request text after IPC writes - ([e179757](https://github.com/baliestri/pinentry-for-windows/commit/e179757fcd1da1832afefc068a72e8b0d130fa9f))
- *(ui)* Set fixed credential titles for Windows and WSL - ([d7740fd](https://github.com/baliestri/pinentry-for-windows/commit/d7740fd1db7b86d118f6bee722ff69542998c51e))
- *(wsl)* Support streamed setup and activation guidance - ([41cbf58](https://github.com/baliestri/pinentry-for-windows/commit/41cbf587e57b3693e30dedbdb9291e9fc11bd761))

### 🚜 Refactor

- *(tests)* Centralize xUnit imports - ([b2a76e0](https://github.com/baliestri/pinentry-for-windows/commit/b2a76e033f58ff2bed7b52dd2fc9b96ba99bba05))
- *(tools)* Rename installers to setup scripts - ([fc2dc18](https://github.com/baliestri/pinentry-for-windows/commit/fc2dc182fe874c98dec4b47a520fef70db734eae))
- *(tools)* Move installer scripts into tools directory - ([9bf567d](https://github.com/baliestri/pinentry-for-windows/commit/9bf567da8ab45fd028dacebfb15e233ff7eaae9e))

### 📚 Documentation

- *(project)* Update pinentry package description - ([e9642c8](https://github.com/baliestri/pinentry-for-windows/commit/e9642c8f2039fd032d1b8d6b4b4e4361b1703218))
- *(readme)* Document remote setup entrypoints - ([a7bc924](https://github.com/baliestri/pinentry-for-windows/commit/a7bc924f737995a7dee1494060c01fc72d7ac03f))
- *(readme)* Add prompt screenshots and simplify setup guide - ([0b7e4ef](https://github.com/baliestri/pinentry-for-windows/commit/0b7e4efe2e80a347ae34420195f7402621f619a7))
- *(readme)* Document CLI, Assuan and verbose diagnostics - ([c063983](https://github.com/baliestri/pinentry-for-windows/commit/c063983c85d357473db89bdb59b77d89fcdc8194))
- *(readme)* Document local benchmarks and release checks - ([bffa5b4](https://github.com/baliestri/pinentry-for-windows/commit/bffa5b44f5f9c2e6c0449956ad04fb305d60aa1b))
- *(readme)* Document complete uninstall flow - ([72126a1](https://github.com/baliestri/pinentry-for-windows/commit/72126a1f697af2dac08b2b4a0b24d92b0005d424))

### 🎨 Styling

- *(editor)* Align formatting and solution dictionary - ([3b0c6f6](https://github.com/baliestri/pinentry-for-windows/commit/3b0c6f69c09d5a879378f1042f8865f6584bfb5f))
- *(project)* Separate property and item groups - ([ef2f63f](https://github.com/baliestri/pinentry-for-windows/commit/ef2f63ffdc90ce6d59d75ef9afddab59bf526ec4))
- Format scripts, tests and existing documentation - ([e0c6451](https://github.com/baliestri/pinentry-for-windows/commit/e0c64515c2754b9d3393e3d82214067d2794a7e6))

### 🧪 Testing

- *(broker)* Cover revoked client handshake - ([93aea26](https://github.com/baliestri/pinentry-for-windows/commit/93aea2668796b3cc90ba1246366e187b37330f78))
- *(gpg)* Add isolated OpenPGP card smoke modes - ([9958a05](https://github.com/baliestri/pinentry-for-windows/commit/9958a0550988310ea213bddfbb82ccf638b2a386))

### ⚙️ Miscellaneous Tasks

- *(release)* Promote only validated release candidates - ([dc9f504](https://github.com/baliestri/pinentry-for-windows/commit/dc9f5045769717bc9c808380836cae53c95ac520))
- *(release)* Validate native x64 and arm64 artifacts - ([98d67d5](https://github.com/baliestri/pinentry-for-windows/commit/98d67d5d1ea1e04ffcb173c6e96a62ea1e197602))
- *(release)* Publish native x64 and arm64 assets - ([3df65d2](https://github.com/baliestri/pinentry-for-windows/commit/3df65d21172ac1925c46ff47bc560bd7e368f4a7))
- *(rider)* Track project metadata - ([a0f39f9](https://github.com/baliestri/pinentry-for-windows/commit/a0f39f91ad2b51e7986447d37fece00b5b56487a))
- *(solution)* List validation scripts - ([9c3ac6a](https://github.com/baliestri/pinentry-for-windows/commit/9c3ac6acaa3874448970a0a280386139461bc002))
- *(test)* Validate pushes to develop and main - ([71ff9ed](https://github.com/baliestri/pinentry-for-windows/commit/71ff9ede1f7a238f3848216dd93ac36e37f84f94))
- *(test)* Allow manual native validation runs - ([9118846](https://github.com/baliestri/pinentry-for-windows/commit/91188460fef2220539fb2e7cf7a3e7edf8b85095))
- *(test)* Validate native binaries on pull requests - ([ea246dd](https://github.com/baliestri/pinentry-for-windows/commit/ea246dde0663ffc6951923fa88d6da4fa9e835d9))
- *(tools)* Remove ReSharper tool manifest - ([d8799e8](https://github.com/baliestri/pinentry-for-windows/commit/d8799e8d4df771a09dc000f7dbecfa6606b0fe27))
- *(tools)* Remove unused cleanup script - ([97397cc](https://github.com/baliestri/pinentry-for-windows/commit/97397ccf4c0cd3be12c1e5780362514b7e775c2e))

## [1.2.1](https://github.com/baliestri/pinentry-for-windows/compare/v1.2.0..1.2.1) - 2026-10-11

### 🐛 Bug Fixes

- SSH fails using gpg-agent authentication - ([8f6750b](https://github.com/baliestri/pinentry-for-windows/commit/8f6750bfade00a8267943fa6e3c1c3e48c8f452e))

## New Contributors ❤️

- @RichardSlater made their first contribution

## [1.2.0](https://github.com/baliestri/pinentry-for-windows/compare/v1.1.0..v1.2.0) - 2026-07-21

### 🐛 Bug Fixes

- *(getpin)* Catch credential prompt exceptions to prevent silent connection drop -
  ([f87819e](https://github.com/baliestri/pinentry-for-windows/commit/f87819e84d58a4b530c154b75cb33ff809f35016))

## [1.1.0](https://github.com/baliestri/pinentry-for-windows/compare/v1.0.0..v1.1.0) - 2026-06-28

### ⛰️ Features

- *(cache)* Add user-controlled passphrase cache opt-in via CredUI checkbox -
  ([5ac0c2f](https://github.com/baliestri/pinentry-for-windows/commit/5ac0c2f96cde25ffe7d40c586383cea05a5531de))
- *(cache)* Encrypt cached passphrases with DPAPI before PasswordVault storage -
  ([f06b154](https://github.com/baliestri/pinentry-for-windows/commit/f06b1541bcf3ee5da667faa5739d263f73473a7a))
- *(cli)* Add --check diagnostic and --clear-cache commands -
  ([7adb4dc](https://github.com/baliestri/pinentry-for-windows/commit/7adb4dc97b385d7b7fa2b157b14f7fa3443429bc))
- *(dialog)* Add timeout support for confirmation and message dialogs -
  ([3408be8](https://github.com/baliestri/pinentry-for-windows/commit/3408be87490488d7ac265a5c3c9ebefa6f772393))
- *(dialog)* Implement prompt timeout support -
  ([5cec24d](https://github.com/baliestri/pinentry-for-windows/commit/5cec24d1171db942f5e1c7b5b1149988f57d0d49))
- *(docs)* Add installation instructions to README -
  ([dc22eb8](https://github.com/baliestri/pinentry-for-windows/commit/dc22eb8ad685834756e0984c5830a0df4fb4917a))
- *(install)* Verify release asset checksums during installation -
  ([2fdbdc9](https://github.com/baliestri/pinentry-for-windows/commit/2fdbdc9a611a1967042e670713994aa765277cce))
- *(install)* Add update and uninstall options to installer -
  ([6deb81b](https://github.com/baliestri/pinentry-for-windows/commit/6deb81b7ea3bb95b6e39f85e26ecf052614bc7d0))
- *(install)* Add PowerShell installer for pinentry -
  ([8330650](https://github.com/baliestri/pinentry-for-windows/commit/833065074f963e968d46826d6b3e48c7abc75122))
- *(security)* Redact PromptResult.ToString () and clear intermediate passphrase buffers -
  ([e127c04](https://github.com/baliestri/pinentry-for-windows/commit/e127c0446968f39351128d08e8479bdba24409f5))
- *(setkeyinfo)* Parse X/[keygrip] format and expose type and keygrip in session state -
  ([9a65ee6](https://github.com/baliestri/pinentry-for-windows/commit/9a65ee6dc505dd0664e22b53bf937d6e09994076))
- *(test)* Add live decrypt, cache-hit, and cache-clear GPG scenarios -
  ([92169d0](https://github.com/baliestri/pinentry-for-windows/commit/92169d0f9e21a89787da84b6b4c7ec4b5ba60df0))

### 🐛 Bug Fixes

- *(cli)* Normalize path separators in gnupg pinentry-program check -
  ([524d951](https://github.com/baliestri/pinentry-for-windows/commit/524d95191952b76e0d63fc233680bdc6da0b87a9))
- *(gpg-key-tests)* Generate passphrase-protected key to enable Windows Hello path -
  ([797db83](https://github.com/baliestri/pinentry-for-windows/commit/797db833c054dab1ce48c29519e2d7985d22e0df))

### 🚜 Refactor

- *(tools)* Rename format.ps1 to cleanup.ps1 and add -All/-Modified switches -
  ([310dbc6](https://github.com/baliestri/pinentry-for-windows/commit/310dbc644504ee381817b5a1bbfdd3c201361aa4))

### 📚 Documentation

- *(gpg-agent)* Add compatibility test suite documentation -
  ([c6adadd](https://github.com/baliestri/pinentry-for-windows/commit/c6adaddb5a0aabbbd3b302991259da64667aaf90))
- *(gpg-agent-compatibility-tests)* Document new live test switches and trace capture -
  ([2b5a5d1](https://github.com/baliestri/pinentry-for-windows/commit/2b5a5d16d811e0a2a380a4ede62632751b60738b))
- *(installation)* Update README with installer options and examples -
  ([de42f7f](https://github.com/baliestri/pinentry-for-windows/commit/de42f7ff1ca555872f7312c59db4f565e23077f0))
- *(key-identity)* Add GPG key identity design document and README link -
  ([23504a4](https://github.com/baliestri/pinentry-for-windows/commit/23504a4ba7971f01135598190b82a1cf9b13d22b))
- *(readme)* Document --check and --clear-cache CLI commands -
  ([b543178](https://github.com/baliestri/pinentry-for-windows/commit/b54317837230044e45d0e699dd8caba9a3c30880))
- *(readme)* Add preview screenshot -
  ([3ed45f2](https://github.com/baliestri/pinentry-for-windows/commit/3ed45f2030ffb0a74b4464a7ce29b8d5f9fb070f))
- *(readme)* Document user cache opt-in feature and settings.json -
  ([e4108ab](https://github.com/baliestri/pinentry-for-windows/commit/e4108abf88207c39cefe55bd8b2c1e0e1955b655))
- *(readme)* Link to security model document -
  ([df42451](https://github.com/baliestri/pinentry-for-windows/commit/df4245199f50c9ae41196e53dc982d65fa4b025c))
- *(security)* Update security model to reflect DPAPI envelope storage -
  ([4a622a7](https://github.com/baliestri/pinentry-for-windows/commit/4a622a711708af114298d9b594f56611df88df02))
- *(security)* Add security model document -
  ([9c56e45](https://github.com/baliestri/pinentry-for-windows/commit/9c56e45a85a24656a9a46c32089e9afc36de2035))
- *(security-model)* Document managed buffer improvements and remaining limitations -
  ([4eedb65](https://github.com/baliestri/pinentry-for-windows/commit/4eedb65b70d18ac9289b7da1759f88edbe0036b9))
- Document smart card cache bypass and Windows Hello behavior -
  ([11798a3](https://github.com/baliestri/pinentry-for-windows/commit/11798a32eb86f338c3ff313dcee7b298bcc555a9))

### 🎨 Styling

- Apply ReSharper full cleanup profile across codebase -
  ([bf7bd22](https://github.com/baliestri/pinentry-for-windows/commit/bf7bd222f1f5f04f9c1bc3f075ac53f779e10712))

### 🧪 Testing

- *(cache)* Cover user cache opt-in scenarios in GetPinCommandHandler -
  ([cf8a974](https://github.com/baliestri/pinentry-for-windows/commit/cf8a9749736075f1cb0305bd36d9414f6a1fb2b3))
- *(cache)* Add CacheEntryProtector round-trip and format tests -
  ([11e81cc](https://github.com/baliestri/pinentry-for-windows/commit/11e81cc18bf7ee8a12354708e7a334bc343b5251))
- *(cli)* Add process-level tests for --check and --clear-cache -
  ([26e8f7a](https://github.com/baliestri/pinentry-for-windows/commit/26e8f7a00a0f29ee1e5dfb66cd100a9cbfbb91dc))
- *(dialog)* Add timeout handling tests for message and confirm -
  ([e9aa975](https://github.com/baliestri/pinentry-for-windows/commit/e9aa975986028da8180dc5532a0abdbf2f113ac7))
- *(gpg-agent)* Add compatibility test suite for GPG agent sequences -
  ([93f00cc](https://github.com/baliestri/pinentry-for-windows/commit/93f00cc7615d81cff1f0e779444942819989ed5e))
- *(security)* Verify PromptResult.ToString () does not expose password -
  ([261613d](https://github.com/baliestri/pinentry-for-windows/commit/261613dcd71421395e6fc52d1a277020305a757f))

### ⚙️ Miscellaneous Tasks

- *(gitignore)* Remove Docker-related entries -
  ([e515c88](https://github.com/baliestri/pinentry-for-windows/commit/e515c8877728e401b860ecb9d0a7758dc670fa58))
- *(tests)* Mark test project as non-publishable -
  ([5f19c1e](https://github.com/baliestri/pinentry-for-windows/commit/5f19c1e706e1545025f2bb8b33580a179891138e))
- *(tools)* Add ReSharper dotnet tool manifest and format script -
  ([4b8d6b7](https://github.com/baliestri/pinentry-for-windows/commit/4b8d6b76b4fa44ab871f459c2d64eb92a8912cb7))

## [1.0.0] - 2026-06-04

### ⛰️ Features

- *(commands)* Implement pinentry Assuan handlers -
  ([e7cb1ba](https://github.com/baliestri/pinentry-for-windows/commit/e7cb1ba40eedf149bd922a9a5bf66b4d2edca850))
- *(transport)* Add console Assuan transport -
  ([223bb40](https://github.com/baliestri/pinentry-for-windows/commit/223bb402398efdc911d5b2d28fe997f5c46cd8c7))
- *(windows)* Add credential and dialog services -
  ([62faa71](https://github.com/baliestri/pinentry-for-windows/commit/62faa71970d3476743953929269c2a0f8f4be74c))
- Wire Assuan pinentry server -
  ([a403a35](https://github.com/baliestri/pinentry-for-windows/commit/a403a35446fe00f744a8bb24c5d6afa577d72a01))

### 🐛 Bug Fixes

- *(commands)* Correct spelling of UNKNOWN_VALUE in error responses -
  ([f3626f2](https://github.com/baliestri/pinentry-for-windows/commit/f3626f21336fb05aeabfd5c81e1e455c3aa741f0))

### 🚜 Refactor

- *(credentials)* Update credential handling with PInvoke -
  ([5eb45d9](https://github.com/baliestri/pinentry-for-windows/commit/5eb45d9dbf44479cfce5ff32a6e2e59a1457a7da))
- *(dialog)* Update to use CsWin32-generated TaskDialogIndirect -
  ([1465acb](https://github.com/baliestri/pinentry-for-windows/commit/1465acbb3e9a664bbce7e2e574f0a64ab5c37637))

### 📚 Documentation

- *(readme)* Update project description and features list -
  ([05f14e2](https://github.com/baliestri/pinentry-for-windows/commit/05f14e2422afdd89493e22abb5205e4c2d5b647d))
- Add CHANGELOG file -
  ([d3658a2](https://github.com/baliestri/pinentry-for-windows/commit/d3658a2e128c83eea08dec7da155f3541a7d5d26))

### 🧪 Testing

- *(tests)* Add comprehensive smoke tests for PinentryForWindows -
  ([2790e9f](https://github.com/baliestri/pinentry-for-windows/commit/2790e9fbfae383bca82daf76dd6cd1c81e45441b))
- Add pinentry command and transport coverage -
  ([c5aa31a](https://github.com/baliestri/pinentry-for-windows/commit/c5aa31ae24c69b2c323aaa36dee65d26cba7b6a8))

### ⚙️ Miscellaneous Tasks

- *(release-tag)* Publish Windows binaries and generate checksums -
  ([af69d69](https://github.com/baliestri/pinentry-for-windows/commit/af69d695daa1bf063bc9688fa3ba23dd73e6f98e))

### Build

- *(dependencies)* Update Microsoft.Windows.CsWin32 package configuration -
  ([5f03b83](https://github.com/baliestri/pinentry-for-windows/commit/5f03b833c31da04656b4bc48332327457eb3e2cc))
- *(versioning)* Add Git versioning support in build process -
  ([31e9337](https://github.com/baliestri/pinentry-for-windows/commit/31e93375775d99cb1b15d775508f10150044a198))
- Configure native Windows publish -
  ([204dcd2](https://github.com/baliestri/pinentry-for-windows/commit/204dcd2412f52a4ca59b8ad40e9bbfc9a7704e4a))

## New Contributors ❤️

* @baliestri made their first contribution
