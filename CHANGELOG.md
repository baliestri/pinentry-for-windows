# Pinentry For Windows Changelog

## [1.2.0](https://github.com/baliestri/pinentry-for-windows/compare/v1.1.0..v1.2.0) - 2026-07-21

### 🐛 Bug Fixes

- *(getpin)* Catch credential prompt exceptions to prevent silent connection drop - ([f87819e](https://github.com/baliestri/pinentry-for-windows/commit/f87819e84d58a4b530c154b75cb33ff809f35016))

## [1.1.0](https://github.com/baliestri/pinentry-for-windows/compare/v1.0.0..v1.1.0) - 2026-06-28

### ⛰️  Features

- *(cache)* Add user-controlled passphrase cache opt-in via CredUI checkbox - ([5ac0c2f](https://github.com/baliestri/pinentry-for-windows/commit/5ac0c2f96cde25ffe7d40c586383cea05a5531de))
- *(cache)* Encrypt cached passphrases with DPAPI before PasswordVault storage - ([f06b154](https://github.com/baliestri/pinentry-for-windows/commit/f06b1541bcf3ee5da667faa5739d263f73473a7a))
- *(cli)* Add --check diagnostic and --clear-cache commands - ([7adb4dc](https://github.com/baliestri/pinentry-for-windows/commit/7adb4dc97b385d7b7fa2b157b14f7fa3443429bc))
- *(dialog)* Add timeout support for confirmation and message dialogs - ([3408be8](https://github.com/baliestri/pinentry-for-windows/commit/3408be87490488d7ac265a5c3c9ebefa6f772393))
- *(dialog)* Implement prompt timeout support - ([5cec24d](https://github.com/baliestri/pinentry-for-windows/commit/5cec24d1171db942f5e1c7b5b1149988f57d0d49))
- *(docs)* Add installation instructions to README - ([dc22eb8](https://github.com/baliestri/pinentry-for-windows/commit/dc22eb8ad685834756e0984c5830a0df4fb4917a))
- *(install)* Verify release asset checksums during installation - ([2fdbdc9](https://github.com/baliestri/pinentry-for-windows/commit/2fdbdc9a611a1967042e670713994aa765277cce))
- *(install)* Add update and uninstall options to installer - ([6deb81b](https://github.com/baliestri/pinentry-for-windows/commit/6deb81b7ea3bb95b6e39f85e26ecf052614bc7d0))
- *(install)* Add PowerShell installer for pinentry - ([8330650](https://github.com/baliestri/pinentry-for-windows/commit/833065074f963e968d46826d6b3e48c7abc75122))
- *(security)* Redact PromptResult.ToString() and clear intermediate passphrase buffers - ([e127c04](https://github.com/baliestri/pinentry-for-windows/commit/e127c0446968f39351128d08e8479bdba24409f5))
- *(setkeyinfo)* Parse X/[keygrip] format and expose type and keygrip in session state - ([9a65ee6](https://github.com/baliestri/pinentry-for-windows/commit/9a65ee6dc505dd0664e22b53bf937d6e09994076))
- *(test)* Add live decrypt, cache-hit, and cache-clear GPG scenarios - ([92169d0](https://github.com/baliestri/pinentry-for-windows/commit/92169d0f9e21a89787da84b6b4c7ec4b5ba60df0))

### 🐛 Bug Fixes

- *(cli)* Normalize path separators in gnupg pinentry-program check - ([524d951](https://github.com/baliestri/pinentry-for-windows/commit/524d95191952b76e0d63fc233680bdc6da0b87a9))
- *(gpg-key-tests)* Generate passphrase-protected key to enable Windows Hello path - ([797db83](https://github.com/baliestri/pinentry-for-windows/commit/797db833c054dab1ce48c29519e2d7985d22e0df))

### 🚜 Refactor

- *(tools)* Rename format.ps1 to cleanup.ps1 and add -All/-Modified switches - ([310dbc6](https://github.com/baliestri/pinentry-for-windows/commit/310dbc644504ee381817b5a1bbfdd3c201361aa4))

### 📚 Documentation

- *(gpg-agent)* Add compatibility test suite documentation - ([c6adadd](https://github.com/baliestri/pinentry-for-windows/commit/c6adaddb5a0aabbbd3b302991259da64667aaf90))
- *(gpg-agent-compatibility-tests)* Document new live test switches and trace capture - ([2b5a5d1](https://github.com/baliestri/pinentry-for-windows/commit/2b5a5d16d811e0a2a380a4ede62632751b60738b))
- *(installation)* Update README with installer options and examples - ([de42f7f](https://github.com/baliestri/pinentry-for-windows/commit/de42f7ff1ca555872f7312c59db4f565e23077f0))
- *(key-identity)* Add GPG key identity design document and README link - ([23504a4](https://github.com/baliestri/pinentry-for-windows/commit/23504a4ba7971f01135598190b82a1cf9b13d22b))
- *(readme)* Document --check and --clear-cache CLI commands - ([b543178](https://github.com/baliestri/pinentry-for-windows/commit/b54317837230044e45d0e699dd8caba9a3c30880))
- *(readme)* Add preview screenshot - ([3ed45f2](https://github.com/baliestri/pinentry-for-windows/commit/3ed45f2030ffb0a74b4464a7ce29b8d5f9fb070f))
- *(readme)* Document user cache opt-in feature and settings.json - ([e4108ab](https://github.com/baliestri/pinentry-for-windows/commit/e4108abf88207c39cefe55bd8b2c1e0e1955b655))
- *(readme)* Link to security model document - ([df42451](https://github.com/baliestri/pinentry-for-windows/commit/df4245199f50c9ae41196e53dc982d65fa4b025c))
- *(security)* Update security model to reflect DPAPI envelope storage - ([4a622a7](https://github.com/baliestri/pinentry-for-windows/commit/4a622a711708af114298d9b594f56611df88df02))
- *(security)* Add security model document - ([9c56e45](https://github.com/baliestri/pinentry-for-windows/commit/9c56e45a85a24656a9a46c32089e9afc36de2035))
- *(security-model)* Document managed buffer improvements and remaining limitations - ([4eedb65](https://github.com/baliestri/pinentry-for-windows/commit/4eedb65b70d18ac9289b7da1759f88edbe0036b9))
- Document smart card cache bypass and Windows Hello behavior - ([11798a3](https://github.com/baliestri/pinentry-for-windows/commit/11798a32eb86f338c3ff313dcee7b298bcc555a9))

### 🎨 Styling

- Apply ReSharper full cleanup profile across codebase - ([bf7bd22](https://github.com/baliestri/pinentry-for-windows/commit/bf7bd222f1f5f04f9c1bc3f075ac53f779e10712))

### 🧪 Testing

- *(cache)* Cover user cache opt-in scenarios in GetPinCommandHandler - ([cf8a974](https://github.com/baliestri/pinentry-for-windows/commit/cf8a9749736075f1cb0305bd36d9414f6a1fb2b3))
- *(cache)* Add CacheEntryProtector round-trip and format tests - ([11e81cc](https://github.com/baliestri/pinentry-for-windows/commit/11e81cc18bf7ee8a12354708e7a334bc343b5251))
- *(cli)* Add process-level tests for --check and --clear-cache - ([26e8f7a](https://github.com/baliestri/pinentry-for-windows/commit/26e8f7a00a0f29ee1e5dfb66cd100a9cbfbb91dc))
- *(dialog)* Add timeout handling tests for message and confirm - ([e9aa975](https://github.com/baliestri/pinentry-for-windows/commit/e9aa975986028da8180dc5532a0abdbf2f113ac7))
- *(gpg-agent)* Add compatibility test suite for GPG agent sequences - ([93f00cc](https://github.com/baliestri/pinentry-for-windows/commit/93f00cc7615d81cff1f0e779444942819989ed5e))
- *(security)* Verify PromptResult.ToString() does not expose password - ([261613d](https://github.com/baliestri/pinentry-for-windows/commit/261613dcd71421395e6fc52d1a277020305a757f))

### ⚙️ Miscellaneous Tasks

- *(gitignore)* Remove Docker-related entries - ([e515c88](https://github.com/baliestri/pinentry-for-windows/commit/e515c8877728e401b860ecb9d0a7758dc670fa58))
- *(tests)* Mark test project as non-publishable - ([5f19c1e](https://github.com/baliestri/pinentry-for-windows/commit/5f19c1e706e1545025f2bb8b33580a179891138e))
- *(tools)* Add ReSharper dotnet tool manifest and format script - ([4b8d6b7](https://github.com/baliestri/pinentry-for-windows/commit/4b8d6b76b4fa44ab871f459c2d64eb92a8912cb7))

## [1.0.0] - 2026-06-04

### ⛰️  Features

- *(commands)* Implement pinentry Assuan handlers - ([e7cb1ba](https://github.com/baliestri/pinentry-for-windows/commit/e7cb1ba40eedf149bd922a9a5bf66b4d2edca850))
- *(transport)* Add console Assuan transport - ([223bb40](https://github.com/baliestri/pinentry-for-windows/commit/223bb402398efdc911d5b2d28fe997f5c46cd8c7))
- *(windows)* Add credential and dialog services - ([62faa71](https://github.com/baliestri/pinentry-for-windows/commit/62faa71970d3476743953929269c2a0f8f4be74c))
- Wire Assuan pinentry server - ([a403a35](https://github.com/baliestri/pinentry-for-windows/commit/a403a35446fe00f744a8bb24c5d6afa577d72a01))

### 🐛 Bug Fixes

- *(commands)* Correct spelling of UNKNOWN_VALUE in error responses - ([f3626f2](https://github.com/baliestri/pinentry-for-windows/commit/f3626f21336fb05aeabfd5c81e1e455c3aa741f0))

### 🚜 Refactor

- *(credentials)* Update credential handling with PInvoke - ([5eb45d9](https://github.com/baliestri/pinentry-for-windows/commit/5eb45d9dbf44479cfce5ff32a6e2e59a1457a7da))
- *(dialog)* Update to use CsWin32-generated TaskDialogIndirect - ([1465acb](https://github.com/baliestri/pinentry-for-windows/commit/1465acbb3e9a664bbce7e2e574f0a64ab5c37637))

### 📚 Documentation

- *(readme)* Update project description and features list - ([05f14e2](https://github.com/baliestri/pinentry-for-windows/commit/05f14e2422afdd89493e22abb5205e4c2d5b647d))
- Add CHANGELOG file - ([d3658a2](https://github.com/baliestri/pinentry-for-windows/commit/d3658a2e128c83eea08dec7da155f3541a7d5d26))

### 🧪 Testing

- *(tests)* Add comprehensive smoke tests for PinentryForWindows - ([2790e9f](https://github.com/baliestri/pinentry-for-windows/commit/2790e9fbfae383bca82daf76dd6cd1c81e45441b))
- Add pinentry command and transport coverage - ([c5aa31a](https://github.com/baliestri/pinentry-for-windows/commit/c5aa31ae24c69b2c323aaa36dee65d26cba7b6a8))

### ⚙️ Miscellaneous Tasks

- *(release-tag)* Publish Windows binaries and generate checksums - ([af69d69](https://github.com/baliestri/pinentry-for-windows/commit/af69d695daa1bf063bc9688fa3ba23dd73e6f98e))

### Build

- *(dependencies)* Update Microsoft.Windows.CsWin32 package configuration - ([5f03b83](https://github.com/baliestri/pinentry-for-windows/commit/5f03b833c31da04656b4bc48332327457eb3e2cc))
- *(versioning)* Add Git versioning support in build process - ([31e9337](https://github.com/baliestri/pinentry-for-windows/commit/31e93375775d99cb1b15d775508f10150044a198))
- Configure native Windows publish - ([204dcd2](https://github.com/baliestri/pinentry-for-windows/commit/204dcd2412f52a4ca59b8ad40e9bbfc9a7704e4a))

## New Contributors ❤️

* @baliestri made their first contribution
