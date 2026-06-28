// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Logging;
using AssuanLibrary.Server;
using PinentryForWindows.Cli;
using PinentryForWindows.Commands;
using PinentryForWindows.Configuration;
using PinentryForWindows.Logging;
using PinentryForWindows.Transport;
using PinentryForWindows.Transport.Endpoints;

if (args.Length > 0) {
  AppConfiguration.Load();
  return await CliDispatcher.RunAsync(args);
}

AppConfiguration.Load();

using var cts = new CancellationTokenSource();

var loggingOptions = new AssuanLoggingOptions {
  Logger = FileLogger.CreateDefault(),
  PayloadMode = AssuanPayloadLoggingMode.Redacted
};

var server = new AssuanServer(ConsoleListenerFactory.Standard, new AssuanServerOptions {
  Banner = "Pinentry for Windows", // PS: GnuPG uses something like "good to see you again <process id>"
  Logging = loggingOptions
});

server.RegisterCommandHandler<ByeCommandHandler>();
server.RegisterCommandHandler<ClearPassphraseCommandHandler>();
server.RegisterCommandHandler<ConfirmCommandHandler>();
server.RegisterCommandHandler<GetInfoCommandHandler>();
server.RegisterCommandHandler<GetPinCommandHandler>();
server.RegisterCommandHandler<MessageCommandHandler>();
server.RegisterCommandHandler<OptionCommandHandler>();
server.RegisterCommandHandler<ResetCommandHandler>();
server.RegisterCommandHandler<SetCancelButtonCommandHandler>();
server.RegisterCommandHandler<SetDescriptionCommandHandler>();
server.RegisterCommandHandler<SetErrorCommandHandler>();
server.RegisterCommandHandler<SetKeyInfoCommandHandler>();
server.RegisterCommandHandler<SetNotOkButtonCommandHandler>();
server.RegisterCommandHandler<SetOkButtonCommandHandler>();
server.RegisterCommandHandler<SetPromptCommandHandler>();
server.RegisterCommandHandler<SetRepeatCommandHandler>();
server.RegisterCommandHandler<SetRepeatErrorCommandHandler>();
server.RegisterCommandHandler<SetTimeoutCommandHandler>();
server.RegisterCommandHandler<SetTitleCommandHandler>();

var serverTask = server.RunAsync(ConsoleEndpoint.Standard, cts.Token);

var client = new AssuanClient(ConsoleConnectionFactory.Standard, new AssuanClientOptions {
  Logging = loggingOptions
});

await client.ConnectAsync(ConsoleEndpoint.Standard, new Dictionary<string, object>());

if (Console.IsInputRedirected) {
  using var reader = new StreamReader(Console.OpenStandardInput());
  while (await reader.ReadLineAsync() is not null) { }

  cts.Cancel();
}
else {
  Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
  };
}

await serverTask;
return 0;
