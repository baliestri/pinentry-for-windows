// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Relay.Diagnostics;
using PinentryForWindows.Relay.Relay;
using PinentryForWindows.Relay.Security;

if (!OperatingSystem.IsLinux()) {
  Console.Error.WriteLine("The relay requires Linux under WSL2.");
  return 1;
}

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
  e.Cancel = true;
  stop.Cancel();
};

try {
  VerboseLog.Write(VerboseEvent.CommandStarted);
  switch (args) {
    case ["--init", var _]: {
      var identity = new RelayIdentity(args[1]);
      identity.Create();
      Console.WriteLine($"Relay public certificate: {identity.PublicCertificatePath}");
      return 0;
    }
    case ["--trust-broker", var _, var _]:
      new RelayIdentity(args[1]).TrustBroker(args[2]);
      Console.WriteLine("Broker identity pinned.");
      return 0;
    case ["--remove-identity", var _]:
      new RelayIdentity(args[1]).Remove();
      Console.WriteLine("Relay identity removed.");
      return 0;
  }

  string directory;

  if (args is ["--run", var _]) {
    directory = args[1];
  }
  else if (args.Length == 0) {
    directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".config", "PinentryForWindows", "relay");
  }
  else {
    Console.Error.WriteLine("Unsupported relay command.");
    return 2;
  }

  await RelayRunner.RunAsync(new RelayIdentity(directory), stop.Token).ConfigureAwait(false);
  return 0;
}
catch (OperationCanceledException) {
  return 1;
}
catch (Exception error) {
  Console.Error.WriteLine($"Relay unavailable: {error.GetType().Name}.");
  return 1;
}
