using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

public sealed class SampleDelegate : ObjectDelegate {
    private Timer Timer;
    private Random Random;

    private async void TimerCallback(object _) {
        var r = Random.Next(0, 3);
        var obj = r switch {
            0 => new About() as object,
            1 => new Add().Randomize(Random),
            _ => new Subtract().Randomize(Random)
        };
        await Post(obj, default);
    }

    protected sealed override Task<bool> Subscribe(CancellationToken token) {
        Random = new();
        Timer = new(TimerCallback, null, 2500, 500);
        return Task.FromResult(true);
    }

    public sealed override async Task<object> Call(object input, CancellationToken token) {
        await Task.CompletedTask;
        return input switch {
            About _ =>
                new About() { Message = "You can add or subtract!" },
            Operation operation =>
                operation.Result(),
            _ => null
        };
    }

    public sealed override IObjectTypeResolver TypeResolver => ObjectTypeResolver.Lookup(
        GetType().GetNestedTypes());

    public sealed class About {
        public string Message { get; set; }

        public sealed override string ToString() {
            return string.IsNullOrWhiteSpace(Message)
                ? "About"
                : Message;
        }
    }

    public abstract class Operation {
        public double N1 { get; set; }
        public double N2 { get; set; }

        public abstract Result Result();

        public Operation Randomize(Random random) {
            if (random is null) {
                throw new ArgumentNullException(nameof(random));
            }
            N1 = random.Next() + random.NextDouble();
            N2 = random.Next() + random.NextDouble();
            return this;
        }
    }

    public sealed class Add : Operation {
        public sealed override Result Result() {
            return new(N1 + N2);
        }

        public sealed override string ToString() {
            return $"{N1} + {N2}";
        }
    }

    public sealed class Subtract : Operation {
        public sealed override Result Result() {
            return new(N1 - N2);
        }

        public sealed override string ToString() {
            return $"{N1} - {N2}";
        }
    }

    public sealed class Result {
        public double N { get; }

        public Result(double n) {
            N = n;
        }

        public sealed override string ToString() {
            return $"{N}";
        }
    }

    private sealed class Factory : Factory<SampleDelegate> { }
}
