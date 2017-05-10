﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;

using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public static class TracingContext
    {
        private const string SPAN_ID_KEY = "SpanID";
        private const string PARENT_SPAN_ID_KEY = "ParentSpanID";
        private const string ORLEANS_REQUEST_CONTEXT_KEY = "#ORL_RC";
        private const string REQUEST_ID_KEY = "ServiceTraceRequestID";
        private const string OVERRIDES_KEY = "Overrides";


        internal static void SetOverrides(RequestOverrides overrides)
        {
            SetValue(OVERRIDES_KEY, overrides);
        }


        internal static RequestOverrides TryGetOverrides()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY);
        }


        public static string GetHostOverride(string serviceName)
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY)
                ?.Hosts
                ?.SingleOrDefault(o => o.ServiceName == serviceName)
                ?.Host;
        }


        public static void SetHostOverride(string serviceName, string host)
        {
            SetUpStorage();

            var overrides = TryGetValue<RequestOverrides>(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                SetValue(OVERRIDES_KEY, overrides);
            }

            if (overrides.Hosts == null)
                overrides.Hosts = new List<HostOverride>();

            var hostOverride = overrides.Hosts.SingleOrDefault(o => o.ServiceName == serviceName);

            if (hostOverride == null)
            {
                hostOverride = new HostOverride { ServiceName = serviceName };
                overrides.Hosts.Add(hostOverride);
            }

            hostOverride.Host = host;
        }


        public static string TryGetRequestID()
        {            
            return TryGetValue<string>(REQUEST_ID_KEY);
        }

        public static string TryGetSpanID()
        {
            return TryGetValue<string>(SPAN_ID_KEY);
        }

        public static string TryGetParentSpanID()
        {
            return TryGetValue<string>(PARENT_SPAN_ID_KEY);
        }


        /// <summary>
        /// This add requestID to logical call context in unsafe way (no copy on write)
        /// in order to propergate to parent task. From there on it is immutable and safe.
        /// </summary>        
        public static void SetRequestID(string requestID)
        {
            SetValue(REQUEST_ID_KEY, requestID);
        }

        public static void SetSpan(string spanId, string parentSpanId)
        {
            SetValue(SPAN_ID_KEY, spanId);
            SetValue(PARENT_SPAN_ID_KEY, parentSpanId);
        }

        private static void SetValue(string key, object value)
        {
            var context = GetContextData();

            if(context == null)
                throw new InvalidOperationException($"You must call {nameof(SetUpStorage)}() before setting a value on {nameof(TracingContext)}");

            context[key] = value;
        }


        private static T TryGetValue<T>(string key) where T : class
        {
            object value = null;
            GetContextData()?.TryGetValue(key, out value);
            return value as T;
        }


        /// Must setup localstorage in upper most task (one that opens other tasks)
        /// https://stackoverflow.com/questions/31953846/if-i-cant-use-tls-in-c-sharp-async-programming-what-can-i-use
        public static void SetUpStorage()
        {
            if (GetContextData() == null)
            {                
                CallContext.LogicalSetData(ORLEANS_REQUEST_CONTEXT_KEY, new Dictionary<string, object>());
            }
        }


        private static Dictionary<string, object> GetContextData()
        {
            return (Dictionary<string, object>)CallContext.LogicalGetData(ORLEANS_REQUEST_CONTEXT_KEY);
        }
    }
}