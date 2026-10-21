// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using PinentryForWindows.Broker;
using PinentryForWindows.Cache;
using PinentryForWindows.Cli;
using PinentryForWindows.Core.Protocol;
using PinentryForWindows.Diagnostics;
using PinentryForWindows.Transport;
using PinentryForWindows.Ui;

if (args is ["--ui-helper", var _, var _]) {
  return await UiHelper.RunAsync(args[1], args[2]);
}

using var stop = new CancellationTokenSource();
var productVersion = typeof(PinentryPaths).Assembly.GetName().Version?.ToString() ?? "unknown";
Console.CancelKeyPress += (_, e) => {
  e.Cancel = true;
  stop.Cancel();
};

try {
  switch (args) {
    case ["--init-broker", var _]: {
      var identity = new BrokerIdentity(args[1]);
      using var certificate = identity.GetOrCreateServerCertificate();
      Console.WriteLine($"Broker public certificate: {identity.PublicCertificatePath}");
      return 0;
    }
    case ["--approve-client", var _, var _, var _]:
      new BrokerIdentity(args[1]).ApproveClient(args[2], args[3]);
      Console.WriteLine("Client paired.");
      return 0;
    case ["--revoke-client", var _, var _]: {
      using var certificate = X509CertificateLoader.LoadCertificateFromFile(args[2]);
      new BrokerIdentity(args[1]).RevokeClient(certificate);
      Console.WriteLine("Client revoked.");
      return 0;
    }
    case ["--reset-broker", var _]:
      new BrokerIdentity(args[1]).Remove();
      Console.WriteLine("Broker pairing material removed.");
      return 0;
    case ["--vm-id", var _]: {
      var vmId = await VmIdProvider.TryGetAsync(args[1], stop.Token);
      Console.WriteLine(vmId?.ToString("D") ?? "not running");
      return 0;
    }
    case ["--broker", var _]: {
      VerboseLog.Write(VerboseEvent.BrokerStarted);
      var host = new BrokerHost(new BrokerIdentity(args[1]),
        new UiCoordinator(), PinentryPaths.CreateCache(), productVersion);
      await host.RunAsync(stop.Token);
      return 0;
    }
  }

  if (args.Length != 0) {
    VerboseLog.Write(VerboseEvent.CommandStarted);
    return await PinentryCli.RunAsync(args, stop.Token);
  }

  VerboseLog.Write(VerboseEvent.LocalSessionStarted);
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  var nativeEncoding = Encoding.GetEncoding(0, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
  await PinentryProtocol.RunAsync(new StandardIoDuplexStream(Console.OpenStandardInput(), Console.OpenStandardOutput()),
    "windows", true, new UiCoordinator(), PinentryPaths.CreateCache(), stop.Token, productVersion, nativeEncoding);
  return 0;
}
catch (OperationCanceledException) {
  return 1;
}
catch (Exception error) {
  Console.Error.WriteLine($"Pinentry operation failed: {error.GetType().Name}.");
  return 1;
}
