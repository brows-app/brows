using Brows.Localization;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Brows.Agent;

/*
 * Carries out agent requests on the dispatcher that owns the windows. Each request runs under one deadline,
 * from waiting for the dispatcher through input and settling, and no input is delivered after it expires.
 */
internal sealed class AgentOperations {
    private const int DefaultQuiet = 300;
    private const int DefaultTimeout = 10000;
    private const int MaxErrors = 20;
    private const int SettleInterval = 50;
    private const int WaitInterval = 100;

    private static readonly ILog Log = Logging.For(typeof(AgentOperations));

    private long ErrorCursor;

    private Dispatcher Dispatcher { get; }
    private Func<IReadOnlyList<Window>> Windows { get; }
    private Func<ITranslation> Translation { get; }
    private AgentLog AgentLog { get; }

    private static int Positive(int? value, int defaultValue, string name) {
        var result = value ?? defaultValue;
        if (result <= 0) {
            throw new ArgumentException($"Use a positive {name}.");
        }
        return result;
    }

    private static string Required(string value, string name) {
        if (value is null) {
            throw new ArgumentException($"Specify {name}.");
        }
        return value;
    }

    private static void Expect(Window window, string text) {
        var palette = AgentWindow.Palette(window);
        var paletteIsExpected = palette.Focused && string.Equals(palette.Text, text, StringComparison.Ordinal);
        if (!paletteIsExpected) {
            var actual = palette.Focused ? $"holds \"{palette.Text}\"" : "does not have keyboard focus";
            throw new InvalidOperationException(
                $"The palette input {actual} instead of \"{text}\", so no key was pressed.");
        }
    }

    private static void PaletteFocused(Window window) {
        if (!AgentWindow.Palette(window).Focused) {
            throw new InvalidOperationException(
                "The palette input does not have keyboard focus, so the input was stopped.");
        }
    }

    private IReadOnlyList<string> TakeErrors() {
        var errors = AgentLog.Errors(ErrorCursor);
        if (errors.Count == 0) {
            return [];
        }
        ErrorCursor = errors.Max(entry => entry.Seq);
        return errors.TakeLast(MaxErrors).Select(entry => entry.Format()).ToList();
    }

    private Window Target(long? id) {
        var windows = Windows();
        if (id.HasValue) {
            var window = windows.FirstOrDefault(item => AgentWindow.Id(item) == id.Value);
            if (window is null) {
                throw new ArgumentException($"Window {id.Value} is not open.");
            }
            return window;
        }
        var target = windows.FirstOrDefault(item => item.IsActive) ?? windows.LastOrDefault();
        if (target is null) {
            throw new InvalidOperationException("Brows has no open window yet.");
        }
        return target;
    }

    private AgentResponse Observe(Window window) {
        var windows = Windows();
        var index = windows.ToList().IndexOf(window);
        return new AgentResponse {
            Ok = true,
            Window = AgentWindow.Info(window, index),
            Focus = AgentWindow.Focus(window),
            Palette = AgentWindow.Palette(window)
        };
    }

    private ITranslation GestureTranslation() {
        var translation = Translation();
        if (translation is null) {
            throw new InvalidOperationException("Brows is still starting.");
        }
        return translation;
    }

    private async Task<bool> Idle(CancellationToken token) {
        try {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return false;
        }
    }

    private async Task<bool> Delay(int milliseconds, CancellationToken token) {
        try {
            await Task.Delay(milliseconds, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return false;
        }
    }

    private async Task<(bool Settled, string Ui)> Settle(Window window, int quiet, int maxLines,
                                                       CancellationToken token) {
        var ui = AgentUiText.Write(window, maxLines);
        var unchanged = Stopwatch.StartNew();
        for (; ; ) {
            if (!await Idle(token)) {
                return (false, ui);
            }
            var next = AgentUiText.Write(window, maxLines);
            if (next != ui) {
                ui = next;
                unchanged.Restart();
            }
            else if (unchanged.ElapsedMilliseconds >= quiet) {
                return (true, ui);
            }
            if (!await Delay(SettleInterval, token)) {
                return (false, ui);
            }
        }
    }

    private async Task<AgentResponse> Wait(Window window, AgentRequest request, int quiet, int maxLines,
                                           CancellationToken token) {
        var text = request.Text;
        if (text is null) {
            var settle = await Settle(window, quiet, maxLines, token);
            return Observe(window) with { Settled = settle.Settled, Ui = settle.Ui };
        }
        for (; ; ) {
            var idle = await Idle(token);
            var ui = AgentUiText.Write(window, maxLines);
            var found = ui.Contains(text, StringComparison.OrdinalIgnoreCase);
            if (found != request.Absent) {
                return Observe(window) with { Settled = true, Ui = ui };
            }
            if (!idle || !await Delay(WaitInterval, token)) {
                var waitingFor = request.Absent ? $"\"{text}\" to disappear" : $"\"{text}\"";
                return Observe(window) with {
                    Ok = false,
                    Error = $"Timed out waiting for {waitingFor}.",
                    Settled = false,
                    Ui = ui
                };
            }
        }
    }

    private async Task<AgentResponse> Input(Window window, AgentRequest request, Delivery delivery, int quiet,
                                            int maxLines, CancellationToken token) {
        switch (request.Op?.ToLowerInvariant()) {
            case "type": {
                var text = Required(request.Text, "text");
                delivery.Requested = AgentKeyboard.TextElements(text).Count;
                var check = request.RequirePalette ? () => PaletteFocused(window) : default(Action);
                await AgentKeyboard.Type(window, text, count => delivery.Delivered = count, check, token);
                break;
            }
            case "press": {
                var gestures = AgentKeyboard.Parse(Required(request.Keys, "keys"), GestureTranslation());
                delivery.Requested = gestures.Count;
                var expected = request.Expect;
                void check() {
                    if (expected is not null) {
                        Expect(window, expected);
                        expected = null;
                    }
                    if (request.RequirePalette) {
                        PaletteFocused(window);
                    }
                }
                var checkPalette = expected is not null || request.RequirePalette;
                await AgentKeyboard.Press(
                    window, gestures, count => delivery.Delivered = count, checkPalette ? check : null, token);
                break;
            }
        }
        var response = Observe(window) with { Delivered = delivery.Delivered, Requested = delivery.Requested };
        if (request.Settle is false) {
            return response;
        }
        var settle = await Settle(window, quiet, maxLines, token);
        return Observe(window) with {
            Delivered = delivery.Delivered,
            Requested = delivery.Requested,
            Settled = settle.Settled,
            Ui = settle.Ui
        };
    }

    private async Task<AgentResponse> Run(AgentRequest request, Delivery delivery, CancellationToken token) {
        var quiet = request.Quiet ?? DefaultQuiet;
        if (quiet < 0) {
            throw new ArgumentException("Use a quiet time of zero or more milliseconds.");
        }
        var maxLines = Positive(request.MaxLines, AgentUiText.DefaultMaxLines, "maxLines");
        switch (request.Op?.ToLowerInvariant()) {
            case "windows": {
                var windows = Windows();
                var infos = windows.Select((window, index) => AgentWindow.Info(window, index)).ToList();
                var noDefaultWindow = windows.Count == 0 && request.Window is null;
                var target = noDefaultWindow ? null : Target(request.Window);
                var observed = target is null ? new AgentResponse { Ok = true } : Observe(target);
                return observed with { Windows = infos };
            }
            case "type":
            case "press":
                return await Input(Target(request.Window), request, delivery, quiet, maxLines, token);
            case "ui": {
                var window = Target(request.Window);
                return Observe(window) with { Ui = AgentUiText.Write(window, maxLines) };
            }
            case "wait":
                return await Wait(Target(request.Window), request, quiet, maxLines, token);
            case "screenshot": {
                var window = Target(request.Window);
                var path = AgentScreenshot.ValidPath(Required(request.Path, "path"));
                var observation = Observe(window);
                var empty = await AgentScreenshot.Save(window, path, write: null, token).ConfigureAwait(false);
                return observation with { Path = path, Empty = empty };
            }
            case "log":
                return new AgentResponse { Ok = true, Log = AgentLog.Since(request.Since) };
        }
        throw new ArgumentException(
            $"\"{request.Op}\" is not an operation. Use windows, type, press, ui, wait, screenshot, or log.");
    }

    private async Task<AgentResponse> Respond(AgentRequest request, Delivery delivery, int timeout,
                                              CancellationToken token) {
        try {
            return await Dispatcher
                .InvokeAsync(() => Run(request, delivery, token), DispatcherPriority.Normal, token)
                .Task
                .Unwrap();
        }
        catch (Exception) when (Dispatcher.HasShutdownStarted) {
            return AgentResponse.Failure("Brows is shutting down.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            var message = string.Equals(request.Op, "screenshot", StringComparison.OrdinalIgnoreCase)
                ? $"The screenshot timed out after {timeout} ms."
                : delivery.Describe(timeout);
            return AgentResponse.Failure(message);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or InvalidOperationException) {
            return AgentResponse.Failure(exception.Message);
        }
        catch (Exception exception) {
            if (Log.Error()) {
                Log.Error(exception);
            }
            return AgentResponse.Failure($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private AgentResponse Finish(AgentResponse response, Delivery delivery) {
        var deliveryIsMissing = delivery.Requested.HasValue && response.Delivered is null;
        if (deliveryIsMissing) {
            response = response with { Delivered = delivery.Delivered, Requested = delivery.Requested };
        }
        return response with { Errors = TakeErrors() };
    }

    internal AgentOperations(Dispatcher dispatcher, Func<IReadOnlyList<Window>> windows, Func<ITranslation> translation,
                             AgentLog log) {
        Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Windows = windows ?? throw new ArgumentNullException(nameof(windows));
        Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        AgentLog = log ?? throw new ArgumentNullException(nameof(log));
    }

    internal async Task<AgentResponse> Handle(AgentRequest request, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(request);
        var delivery = new Delivery();
        var timeout = request.Timeout ?? DefaultTimeout;
        if (timeout <= 0) {
            return Finish(AgentResponse.Failure("Use a positive timeout."), delivery);
        }
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token)) {
            deadline.CancelAfter(timeout);
            var response = await Respond(request, delivery, timeout, deadline.Token);
            return Finish(response, delivery);
        }
    }

    private sealed class Delivery {
        internal int Delivered { get; set; }
        internal int? Requested { get; set; }

        internal string Describe(int timeout) {
            if (Requested is null || Delivered == 0) {
                return $"The request timed out after {timeout} ms before any input was delivered.";
            }
            if (Delivered < Requested) {
                return $"The request timed out after {timeout} ms, having delivered {Delivered} of {Requested}.";
            }
            return $"The request timed out after {timeout} ms, after all {Requested} were delivered.";
        }
    }
}
