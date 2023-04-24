using System;

namespace Domore.Logs {
    internal sealed class LogServiceFactory {
        public ILogService Create(string name) {
            var serviceNamespace = string.Join(".", GetType().Namespace, "Service");
            var serviceTypeName = string.Join(".", serviceNamespace, name);
            var serviceType =
                Type.GetType(serviceTypeName, throwOnError: false, ignoreCase: true) ??
                Type.GetType(serviceTypeName + "Log", throwOnError: false, ignoreCase: true);
            if (serviceType == null) {
                throw new ArgumentException(paramName: name, message: $"{nameof(name)} [{name}]");
            }
            var instance = Activator.CreateInstance(serviceType);
            var logService = (ILogService)instance;
            return logService;
        }
    }
}
