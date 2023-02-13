using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using CHANNEL = System.Threading.Channels.Channel;

namespace Domore.Collections.Generic.CollectAsyncModes {
    internal static class Channel {
        public static IAsyncEnumerable<IReadOnlyList<TTransform>> CollectAsync<TSource, TTransform>(IEnumerable<TSource> source, CollectAsyncOptions<TSource, TTransform> options, CancellationToken cancellationToken) {
            if (null == source) throw new ArgumentNullException(nameof(source));
            options = options ?? new CollectAsyncOptions<TSource, TTransform>();
            var ch = CHANNEL.CreateUnbounded<IReadOnlyList<TTransform>>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
            var writer = ch.Writer;
            var reader = ch.Reader;
            var now = Environment.TickCount64;
            var skip = options.Skip;
            var ticks = options.Ticks;
            var transform = options.Transform;
            ThreadPool.QueueUserWorkItem(async _ => {
                var error = default(Exception);
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    var list = ticks < 1
                        ? null
                        : new List<TTransform>();
                    foreach (var item in source) {
                        cancellationToken.ThrowIfCancellationRequested();
                        var skipped = skip(item);
                        if (skipped == false) {
                            var transformed = transform(item);
                            if (list == null) {
                                await writer.WriteAsync(new[] { transformed }, cancellationToken);
                            }
                            else {
                                list.Add(transform(item));
                                var elapsed = Environment.TickCount64 - now;
                                if (elapsed > ticks) {
                                    await writer.WriteAsync(list, cancellationToken);
                                    now = Environment.TickCount64;
                                    list = new();
                                }
                            }
                        }
                    }
                    if (list != null && list.Count > 0) {
                        await writer.WriteAsync(list, cancellationToken);
                    }
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                    }
                    else {
                        error = ex;
                    }
                }
                finally {
                    writer.Complete(error);
                }
            });
            return reader.ReadAllAsync(cancellationToken);
        }
    }
}
