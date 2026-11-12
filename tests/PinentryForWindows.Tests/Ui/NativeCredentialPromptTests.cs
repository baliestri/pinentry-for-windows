// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Ui;

namespace PinentryForWindows.Tests.Ui;

public sealed class NativeCredentialPromptTests {
  [Fact]
  public void Description_remains_the_complete_gpg_key_description() {
    var request = new PromptRequest("Unlock", "OpenPGP key\nID ABCDEF0123456789",
      "n/0123456789ABCDEF0123456789ABCDEF01234567", "Passphrase:", false, 0);

    Assert.Equal(request.Description, NativeCredentialPrompt.BuildMessage(request));
  }
}
