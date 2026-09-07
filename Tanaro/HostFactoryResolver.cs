// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs
// (Copyright (c) .NET Foundation and Contributors, MIT License), trimmed to the subset Tanaro needs.

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Tanaro;

// Intercepts an app's real Program.Main without requiring any testing seam in that app: the .NET hosting
// stack (HostBuilder.Build / HostApplicationBuilder.Build, which FunctionsApplicationBuilder.Build delegates
// to) fires "HostBuilding"/"HostBuilt" DiagnosticListener events on every call. We run the entry point on a
// background thread, capture those events, let the caller mutate the builder, and abort before Run() is reached.
internal static class HostFactoryResolver
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);

    public static Func<string[], IHost>? ResolveHostFactory(Assembly assembly, Action<IHostBuilder>? configureHostBuilder = null, TimeSpan waitTimeout = default)
    {
        if (assembly.EntryPoint is null)
        {
            return null;
        }

        var timeout = waitTimeout == default ? DefaultWaitTimeout : waitTimeout;
        return args => new HostingListener(args, assembly.EntryPoint, timeout, configureHostBuilder).CreateHost();
    }

    private sealed class HostingListener(string[] args, MethodInfo entryPoint, TimeSpan waitTimeout, Action<IHostBuilder>? configure)
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private static readonly AsyncLocal<HostingListener?> CurrentListener = new();

        private readonly TaskCompletionSource<IHost> _hostTcs = new();
        private IDisposable? _subscription;

        public IHost CreateHost()
        {
            using var subscription = DiagnosticListener.AllListeners.Subscribe(this);

            var thread = new Thread(RunEntryPoint) { IsBackground = true };
            thread.Start();

            if (!_hostTcs.Task.Wait(waitTimeout))
            {
                throw new InvalidOperationException($"Timed out waiting for the entry point of {entryPoint.DeclaringType} to build an IHost after {waitTimeout}.");
            }

            return _hostTcs.Task.GetAwaiter().GetResult();
        }

        private void RunEntryPoint()
        {
            try
            {
                CurrentListener.Value = this;

                var parameters = entryPoint.GetParameters();
                entryPoint.Invoke(null, parameters.Length == 0 ? [] : [args]);

                _hostTcs.TrySetException(new InvalidOperationException("The entry point exited without ever building an IHost."));
            }
            catch (TargetInvocationException tie) when (tie.InnerException is HostAbortedException)
            {
                // Expected: thrown deliberately once we've captured the built IHost, to stop Main before Run().
            }
            catch (TargetInvocationException tie)
            {
                _hostTcs.TrySetException(tie.InnerException ?? tie);
            }
            catch (Exception ex)
            {
                _hostTcs.TrySetException(ex);
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (CurrentListener.Value != this)
            {
                return;
            }

            if (value.Name == "Microsoft.Extensions.Hosting")
            {
                _subscription = value.Subscribe(this);
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (CurrentListener.Value != this)
            {
                return;
            }

            if (value.Key == "HostBuilding")
            {
                configure?.Invoke((IHostBuilder)value.Value!);
            }
            else if (value.Key == "HostBuilt")
            {
                _hostTcs.TrySetResult((IHost)value.Value!);
                throw new HostAbortedException();
            }
        }

        public void OnCompleted() => _subscription?.Dispose();

        public void OnError(Exception error)
        {
        }
    }

    private sealed class HostAbortedException : Exception;
}
