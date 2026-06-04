# Pinentry For Windows Changelog

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
