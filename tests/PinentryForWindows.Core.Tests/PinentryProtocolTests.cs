// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Tests;

public sealed class PinentryProtocolTests {
  private const string GRIP = "0123456789ABCDEF0123456789ABCDEF01234567";

  [Fact]
  public async Task Software_key_requires_user_opt_in_and_returns_secret_once() {
    var ui = new FakeUi("correct horse", true);
    var cache = new FakeCache();
    var output = await RunAsync($"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nGETPIN\nBYE\n", ui, cache);

    Assert.Contains("D correct horse\nOK", output);
    Assert.Equal(1, ui.PromptCount);
    Assert.True(ui.LastGetPinTimestamp > 0);
    Assert.Equal(1, cache.WriteCount);
    Assert.DoesNotContain("S PASSWORD_FROM_CACHE", output);
  }

  [Fact]
  public async Task Clear_keyinfo_suppresses_cache_even_with_remember_requested() {
    var ui = new FakeUi("card-pin", true);
    var cache = new FakeCache();
    var output = await RunAsync(
      $"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nSETKEYINFO --clear\nGETPIN\nBYE\n", ui, cache);

    Assert.Contains("D card-pin\nOK", output);
    Assert.False(ui.LastOfferSave);
    Assert.Equal(0, cache.ReadCount);
    Assert.Equal(0, cache.WriteCount);
  }

  [Fact]
  public async Task Card_description_blocks_cache_even_if_keyinfo_follows_it() {
    var ui = new FakeUi("card-pin", true);
    var cache = new FakeCache();
    var output = await RunAsync($"SETDESC Unlock smart card%0AHolder: Ada\nSETKEYINFO n/{GRIP}\n" +
                                "OPTION allow-external-password-cache\nGETPIN\nBYE\n", ui, cache);

    Assert.Contains("D card-pin\nOK", output);
    Assert.False(ui.LastOfferSave);
    Assert.Equal(0, cache.ReadCount);
    Assert.Equal(0, cache.WriteCount);
  }

  [Fact]
  public async Task Agent_permission_alone_never_persists_a_password() {
    var cache = new FakeCache();
    await RunAsync($"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nGETPIN\nBYE\n",
      new FakeUi("not-saved", false), cache);
    Assert.Equal(0, cache.WriteCount);
  }

  [Fact]
  public async Task Cache_unavailable_never_offers_remember_checkbox() {
    var cache = new FakeCache { IsAvailable = false };
    var ui = new FakeUi("typed", true);
    await RunAsync($"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nGETPIN\nBYE\n", ui, cache);

    Assert.False(ui.LastOfferSave);
    Assert.Equal(0, cache.ReadCount);
    Assert.Equal(0, cache.WriteCount);
  }

  [Fact]
  public async Task Error_skips_one_cache_read_and_does_not_poison_later_requests() {
    var cache = new FakeCache();
    cache.Seed("windows", GRIP, "cached");
    var ui = new FakeUi("corrected", false);
    var output = await RunAsync($"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\n" +
                                "SETERROR Wrong%20passphrase\nGETPIN\nGETPIN\nBYE\n", ui, cache);

    Assert.Contains("D corrected\nOK\nS PASSWORD_FROM_CACHE\nD cached\nOK", output);
    Assert.Equal(1, ui.PromptCount);
    Assert.Equal(1, cache.ReadCount);
  }

  [Fact]
  public async Task Timed_out_ui_returns_pinentry_timeout_error() {
    var ui = new FakeUi("not-used", false) { ShouldTimeOut = true };
    var output = await RunAsync("SETTIMEOUT 1\nGETPIN\nBYE\n", ui, new FakeCache());
    Assert.Contains("ERR 83886142", output);
  }

  [Fact]
  public async Task Confirmation_cancel_and_one_button_message_keep_distinct_semantics() {
    var ui = new FakeUi("unused", false) { ConfirmAnswer = false };
    var output = await RunAsync("CONFIRM\nCONFIRM --one-button\nMESSAGE Information\nBYE\n", ui, new FakeCache());

    Assert.Contains("ERR 83886179", output);
    Assert.Equal(1, ui.ConfirmCount);
    Assert.Equal(2, ui.MessageCount);
  }

  [Fact]
  public async Task Clearpassphrase_revokes_only_the_current_origin() {
    var cache = new FakeCache();
    cache.Seed("windows", GRIP, "local");
    cache.Seed("wsl:archlinux", GRIP, "linux");
    await RunAsync($"CLEARPASSPHRASE {GRIP}\nBYE\n", new FakeUi("unused", false), cache);

    var commands = $"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nGETPIN\nBYE\n";
    var local = await RunAsync(commands, new FakeUi("typed", false), cache);
    var wsl = await RunAsync(commands, new FakeUi("unused", false), cache, "wsl:archlinux");
    Assert.DoesNotContain("S PASSWORD_FROM_CACHE", local);
    Assert.Contains("S PASSWORD_FROM_CACHE\nD linux\nOK", wsl);
  }

  [Fact]
  public async Task Repeat_mismatch_never_returns_a_password() {
    var ui = new FakeUi("unused", false) { Passwords = new Queue<string>(["first", "second"]) };
    var output = await RunAsync("SETREPEAT Repeat%20password\nGETPIN\nBYE\n", ui, new FakeCache());
    Assert.Contains("ERR 83886194", output);
    Assert.DoesNotContain("D first", output);
    Assert.DoesNotContain("D second", output);
  }

  [Fact]
  public async Task Cache_hit_is_scoped_to_authenticated_origin() {
    var cache = new FakeCache();
    cache.Seed("windows", GRIP, "cached");
    const string COMMANDS = $"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\nGETPIN\nBYE\n";

    var windows = await RunAsync(COMMANDS, new FakeUi("unused", false), cache);
    var linuxUi = new FakeUi("from-wsl", false);
    var linux = await RunAsync(COMMANDS, linuxUi, cache, "wsl:archlinux");

    Assert.Contains("S PASSWORD_FROM_CACHE\nD cached\nOK", windows);
    Assert.DoesNotContain("S PASSWORD_FROM_CACHE", linux);
    Assert.Equal(1, linuxUi.PromptCount);
  }

  [Fact]
  public async Task Session_state_does_not_leak_across_connections() {
    var cache = new FakeCache();
    var first = await RunAsync("SETTITLE First\nBYE\n", new FakeUi("x", false), cache);
    var secondUi = new FakeUi("y", false);
    var second = await RunAsync("GETPIN\nBYE\n", secondUi, cache);

    Assert.Contains("OK", first);
    Assert.Contains("D y\nOK", second);
    Assert.Equal("Pinentry for Windows", secondUi.LastTitle);
  }

  [Fact]
  public async Task Host_credential_title_ignores_settitle_even_for_repeat() {
    var ui = new FakeUi("unused", false) { Passwords = new Queue<string>(["same", "same"]) };
    var output = await RunAsync("SETTITLE Client%20supplied\nSETREPEAT Again\nGETPIN\nBYE\n",
      ui, new FakeCache());

    Assert.Contains("S PIN_REPEATED", output);
    Assert.Equal(["Pinentry for Windows", "Pinentry for Windows"], ui.PromptTitles);
  }

  [Fact]
  public async Task Settitle_still_applies_to_confirmation_dialogs() {
    var ui = new FakeUi("unused", false);
    await RunAsync("SETTITLE Confirm%20this\nCONFIRM\nBYE\n", ui, new FakeCache());

    Assert.Equal("Confirm this", ui.LastDialogTitle);
  }

  [Fact]
  public async Task Concurrent_connections_keep_prompts_and_answers_isolated() {
    var bothPrompting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var waiting = 0;

    var windowsUi = new FakeUi("local-only", false) { WaitBeforePrompt = MeetAtPrompt };
    var wslUi = new FakeUi("wsl-only", false) { WaitBeforePrompt = MeetAtPrompt };
    var responses = await Task.WhenAll(
      Task.Run(() => RunAsync("SETDESC Local%20description\nGETPIN\nBYE\n", windowsUi, new FakeCache())),
      Task.Run(() => RunAsync("SETDESC WSL%20description\nGETPIN\nBYE\n", wslUi, new FakeCache(), "wsl:archlinux")));

    Assert.Equal("Pinentry for Windows", windowsUi.LastTitle);
    Assert.Equal("Pinentry for Windows", wslUi.LastTitle);
    Assert.Equal("Local description", windowsUi.LastDescription);
    Assert.Equal("WSL description", wslUi.LastDescription);
    Assert.Contains("D local-only\nOK", responses[0]);
    Assert.DoesNotContain("wsl-only", responses[0]);
    Assert.Contains("D wsl-only\nOK", responses[1]);
    Assert.DoesNotContain("local-only", responses[1]);
    return;

    async Task MeetAtPrompt() {
      if (Interlocked.Increment(ref waiting) == 2) {
        bothPrompting.TrySetResult();
      }

      await bothPrompting.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
  }

  [Fact]
  public async Task Setdesc_preserves_all_decoded_gpg_key_details() {
    var ui = new FakeUi("dummy", false);
    await RunAsync(
      "SETDESC Unlock OpenPGP key:%0A%22Example <example@invalid.test>%22%0AID ABCDEF0123456789\nGETPIN\nBYE\n",
      ui, new FakeCache());

    Assert.Equal("Unlock OpenPGP key:\n\"Example <example@invalid.test>\"\nID ABCDEF0123456789",
      ui.LastDescription);
  }

  [Fact]
  public async Task Setkeyinfo_rejects_multiple_arguments() {
    var output = await RunAsync("SETKEYINFO a b\nBYE\n", new FakeUi("unused", false), new FakeCache());
    Assert.Contains("ERR 67109144", output);
  }

  [Fact]
  public async Task Mixed_encoding_gpg_session_preserves_labels_description_and_cached_secret() {
    var ui = new FakeUi("sésame", true);
    var cache = new FakeCache();
    var commands = Encoding.ASCII.GetBytes($"OPTION allow-external-password-cache\nSETKEYINFO n/{GRIP}\n")
      .Concat(Encoding.Latin1.GetBytes("OPTION default-prompt=Code personnel\u00a0:\n"))
      .Concat(Encoding.UTF8.GetBytes("SETDESC Déverrouiller la clef%0Acréée en 2026 🔑\n"))
      .Concat(Encoding.Latin1.GetBytes("SETPROMPT Phrase secrète\u00a0:\nGETPIN\nGETPIN\nBYE\n"))
      .ToArray();
    var output = await RunAsync(commands, ui, cache, textFallbackEncoding: Encoding.Latin1);

    Assert.DoesNotContain("ERR", output);
    Assert.Contains("D sésame\nOK\nS PASSWORD_FROM_CACHE\nD sésame\nOK", output);
    Assert.Equal("Phrase secrète\u00a0:", ui.LastPromptLabel);
    Assert.Equal("Déverrouiller la clef\ncréée en 2026 🔑", ui.LastDescription);
    Assert.Equal(1, ui.PromptCount);
    Assert.Equal(1, cache.WriteCount);
  }

  [Theory]
  [InlineData(1252, "Phrase secrète\u00a0: €")]
  [InlineData(1251, "Пароль:")]
  [InlineData(932, "パスワード:")]
  public async Task Localized_prompts_use_the_configured_native_code_page(int codePage, string prompt) {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var nativeEncoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    var ui = new FakeUi("typed", false);
    var commands = nativeEncoding.GetBytes($"OPTION default-prompt={prompt}\nGETPIN\nBYE\n");
    var output = await RunAsync(commands, ui, new FakeCache(), textFallbackEncoding: nativeEncoding);

    Assert.DoesNotContain("ERR", output);
    Assert.Equal(prompt, ui.LastPromptLabel);
  }

  [Theory]
  [InlineData("SETPROMPT Phrase secrète\u00a0:", "Phrase secrète\u00a0:")]
  [InlineData("SETPROMPT Phrase secr%E8te%A0:", "Phrase secrète\u00a0:")]
  public async Task Native_prompt_decoding_handles_raw_and_percent_escaped_bytes(string command, string expected) {
    var ui = new FakeUi("typed", false);
    var output = await RunAsync(Encoding.Latin1.GetBytes(command + "\nGETPIN\nBYE\n"), ui, new FakeCache(), textFallbackEncoding: Encoding.Latin1);

    Assert.DoesNotContain("ERR", output);
    Assert.Equal(expected, ui.LastPromptLabel);
  }

  [Fact]
  public async Task Valid_utf8_prompt_takes_precedence_over_native_fallback() {
    var ui = new FakeUi("typed", false);
    var output = await RunAsync(Encoding.UTF8.GetBytes("SETPROMPT Phrase secrète\u00a0: 🔑\nGETPIN\nBYE\n"), ui, new FakeCache(), textFallbackEncoding: Encoding.Latin1);

    Assert.DoesNotContain("ERR", output);
    Assert.Equal("Phrase secrète\u00a0: 🔑", ui.LastPromptLabel);
  }

  [Fact]
  public async Task Unused_localized_options_are_ignored_without_decoding_their_values() {
    var output = await RunAsync(Encoding.Latin1.GetBytes("OPTION default-tt-visi=Rendre la phrase secrète visible\nBYE\n"), new FakeUi("unused", false), new FakeCache(), "wsl:archlinux");

    Assert.DoesNotContain("ERR", output);
  }

  [Fact]
  public async Task Sessions_without_native_fallback_reject_non_utf8_text() {
    var output = await RunAsync(Encoding.Latin1.GetBytes("SETPROMPT Phrase secrète\nBYE\n"), new FakeUi("unused", false), new FakeCache(), "wsl:archlinux");

    Assert.Contains("ERR 67109144 invalid value", output);
  }

  [Theory]
  [InlineData("SETPROMPT invalid%")]
  [InlineData("SETPROMPT invalid%GG")]
  [InlineData("SETKEYINFO n/%E8")]
  [InlineData("SETTIMEOUT %E8")]
  [InlineData("CLEARPASSPHRASE %E8")]
  public async Task Native_text_fallback_preserves_escape_and_identifier_validation(string command) {
    var output = await RunAsync(Encoding.ASCII.GetBytes(command + "\nBYE\n"), new FakeUi("unused", false), new FakeCache(), textFallbackEncoding: Encoding.Latin1);

    Assert.Contains("ERR 67109144", output);
  }

  private static async Task<string> RunAsync(string commands, FakeUi ui, FakeCache cache, string origin = "windows") {
    return await RunAsync(Encoding.UTF8.GetBytes(commands), ui, cache, origin);
  }

  private static async Task<string> RunAsync(byte[] commands, FakeUi ui, FakeCache cache, string origin = "windows", Encoding? textFallbackEncoding = null) {
    await using var stream = new ScriptedStream(commands);
    await PinentryProtocol.RunAsync(stream, origin, true, ui, cache, CancellationToken.None, textFallbackEncoding: textFallbackEncoding);
    return Encoding.UTF8.GetString(stream.Output);
  }

  private sealed class FakeUi(string password, bool remember) : IPinentryUi {
    public int PromptCount { get; private set; }
    public bool LastOfferSave { get; private set; }
    public string? LastTitle { get; private set; }
    public List<string> PromptTitles { get; } = [];
    public string? LastDescription { get; private set; }
    public string? LastPromptLabel { get; private set; }
    public string? LastDialogTitle { get; private set; }
    public long LastGetPinTimestamp { get; private set; }
    public bool ShouldTimeOut { get; init; }
    public bool ConfirmAnswer { get; init; } = true;
    public int ConfirmCount { get; private set; }
    public int MessageCount { get; private set; }
    public Queue<string>? Passwords { get; init; }
    public Func<Task>? WaitBeforePrompt { get; init; }

    public async ValueTask<PasswordPromptResult?> PromptAsync(PromptRequest request, CancellationToken ct) {
      PromptCount++;
      if (ShouldTimeOut) {
        throw new TimeoutException();
      }

      if (WaitBeforePrompt is not null) {
        await WaitBeforePrompt();
      }

      LastOfferSave = request.OfferSave;
      LastTitle = request.Title;
      PromptTitles.Add(request.Title);
      LastDescription = request.Description;
      LastPromptLabel = request.PromptLabel;
      LastGetPinTimestamp = request.GetPinTimestamp;
      var secret = new SecretBuffer(4096);
      secret.Append(Encoding.UTF8.GetBytes(Passwords?.Dequeue() ?? password));
      return new PasswordPromptResult(secret, remember);
    }

    public ValueTask<bool> ConfirmAsync(DialogRequest request, CancellationToken ct) {
      ConfirmCount++;
      LastDialogTitle = request.Title;
      return ValueTask.FromResult(ConfirmAnswer);
    }

    public ValueTask MessageAsync(DialogRequest request, CancellationToken ct) {
      MessageCount++;
      return ValueTask.CompletedTask;
    }
  }

  private sealed class FakeCache : IPinentryCache {
    private readonly Dictionary<(string Origin, string Key), string> _entries = [];
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public bool IsAvailable { get; init; } = true;

    public ValueTask<SecretBuffer?> ReadAsync(string origin, string key, CancellationToken ct) {
      ReadCount++;
      if (!_entries.TryGetValue((origin, key), out var value)) {
        return ValueTask.FromResult<SecretBuffer?>(null);
      }

      var secret = new SecretBuffer(4096);
      secret.Append(Encoding.UTF8.GetBytes(value));
      return ValueTask.FromResult<SecretBuffer?>(secret);
    }

    public ValueTask WriteAsync(string origin, string key, ReadOnlyMemory<byte> secret, CancellationToken ct) {
      WriteCount++;
      _entries[(origin, key)] = Encoding.UTF8.GetString(secret.Span);
      return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string origin, string key, CancellationToken ct) {
      _entries.Remove((origin, key));
      return ValueTask.CompletedTask;
    }

    public void Seed(string origin, string key, string value) => _entries[(origin, key)] = value;
  }

  private sealed class ScriptedStream(byte[] input) : Stream {
    private readonly MemoryStream _output = new();
    private int _position;
    public byte[] Output => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) {
      var size = Math.Min(count, input.Length - _position);
      input.AsSpan(_position, size).CopyTo(buffer.AsSpan(offset));
      _position += size;
      return size;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
      var size = Math.Min(buffer.Length, input.Length - _position);
      input.AsMemory(_position, size).CopyTo(buffer);
      _position += size;
      return ValueTask.FromResult(size);
    }

    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
      => _output.WriteAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin)
      => throw new NotSupportedException();

    public override void SetLength(long value)
      => throw new NotSupportedException();
  }
}
