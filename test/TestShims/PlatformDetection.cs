// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// Subset of dotnet/runtime src/libraries/Common/tests/TestUtilities/System/PlatformDetection*.cs (v11.0.0) with
// exactly the members the imported tests use. Implementations are copied from upstream; the full upstream type
// is not vendored because it drags in interop ([LibraryImport]) and many unrelated dependencies.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    public static partial class PlatformDetection
    {
        public static bool IsNetCore => Environment.Version.Major >= 5 || RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);
        public static bool IsNetFramework => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);
        public static bool IsNotNetFramework => !IsNetFramework;

        public static bool IsMonoRuntime => Type.GetType("Mono.RuntimeStructs") != null;
        public static bool IsNotMonoRuntime => !IsMonoRuntime;
        public static bool IsInterpreter => IsMonoInterpreter || IsCoreClrInterpreter;
        public static bool IsNotInterpreter => !IsInterpreter;
        public static bool IsMonoInterpreter => GetIsRunningOnMonoInterpreter();
        public static bool IsNotMonoInterpreter => !IsMonoInterpreter;
        public static bool IsMonoAOT => Environment.GetEnvironmentVariable("MONO_AOT_MODE") == "aot";
        public static bool IsNativeAot => IsNotMonoRuntime && !IsReflectionEmitSupported;
        public static bool IsNotNativeAot => !IsNativeAot;
        public static bool IsCoreCLR => IsNotMonoRuntime && IsNotNativeAot;
        public static bool IsCoreClrInterpreter => GetIsRunningOnCoreClrInterpreter();
        public static bool IsNotCoreClrInterpreter => !IsCoreClrInterpreter;

        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsAndroid => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID"));
        public static bool IsiOS => RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS"));
        public static bool IstvOS => RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS"));
        public static bool IsMacCatalyst => RuntimeInformation.IsOSPlatform(OSPlatform.Create("MACCATALYST"));
        public static bool IsBrowser => RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
        public static bool IsWasi => RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI"));
        public static bool IsWasm => IsBrowser || IsWasi;
        public static bool IsMobile => IsBrowser || IsWasi || IsAppleMobile || IsAndroid;
        public static bool IsAppleMobile => IsMacCatalyst || IsiOS || IstvOS;

        public static bool IsArmProcess => RuntimeInformation.ProcessArchitecture == Architecture.Arm;
        public static bool IsArm64Process => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        public static bool IsRiscV64Process => (int)RuntimeInformation.ProcessArchitecture == 9; // Architecture.RiscV64;
        public static bool IsX64Process => RuntimeInformation.ProcessArchitecture == Architecture.X64;
        public static bool IsX86Process => RuntimeInformation.ProcessArchitecture == Architecture.X86;
        public static bool IsNotX86Process => !IsX86Process;
        public static bool Is32BitProcess => IntPtr.Size == 4;
        public static bool Is64BitProcess => IntPtr.Size == 8;

        private static readonly Lazy<bool> s_isCheckedRuntime = new Lazy<bool>(() => AssemblyConfigurationEquals("Checked"));
        private static readonly Lazy<bool> s_isReleaseRuntime = new Lazy<bool>(() => AssemblyConfigurationEquals("Release"));
        private static readonly Lazy<bool> s_isDebugRuntime = new Lazy<bool>(() => AssemblyConfigurationEquals("Debug"));

        public static bool IsCheckedRuntime => s_isCheckedRuntime.Value;
        public static bool IsReleaseRuntime => s_isReleaseRuntime.Value;
        public static bool IsDebugRuntime => s_isDebugRuntime.Value;

        // For use as needed on tests that time out when run on a Debug or Checked runtime.
        // Not relevant for timeouts on external activities, such as network timeouts.
        public static int SlowRuntimeTimeoutModifier
        {
            get
            {
                if (IsReleaseRuntime)
                    return 1;
                if (IsRiscV64Process)
                    return IsDebugRuntime ? 10 : 2;
                else
                    return IsDebugRuntime ? 5 : 1;
            }
        }

#if NET11_0_OR_GREATER
        public static bool IsMultithreadingSupported => RuntimeFeature.IsMultithreadingSupported;
#else
        public static bool IsMultithreadingSupported { get; } = GetIsMultithreadingSupported();

        private static bool GetIsMultithreadingSupported()
        {
            if (!IsWasm)
                return true;

            try
            {
                Type runtimeFeatureType = typeof(System.Runtime.CompilerServices.RuntimeFeature);

                PropertyInfo isMultithreadingSupportedProperty = runtimeFeatureType.GetProperty("IsMultithreadingSupported", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (isMultithreadingSupportedProperty != null)
                {
                    return (bool)isMultithreadingSupportedProperty.GetValue(null);
                }
            }
            catch
            {
                // if any of the reflection calls fail, assume multithreading is not supported.
            }
            return false;
        }
#endif
        public static bool IsNotMultithreadingSupported => !IsMultithreadingSupported;

#if NET
        public static bool IsReflectionEmitSupported => RuntimeFeature.IsDynamicCodeSupported;
        public static bool IsNotReflectionEmitSupported => !IsReflectionEmitSupported;
#else
        public static bool IsReflectionEmitSupported => true;
#endif

        public static bool HasAssemblyFiles => !string.IsNullOrEmpty(typeof(PlatformDetection).Assembly.Location);
        public static bool IsBuiltWithAggressiveTrimming => IsNativeAot || IsAppleMobile || IsWasi;

        private static bool GetIsRunningOnMonoInterpreter()
        {
#if NET
            return IsMonoRuntime && RuntimeFeature.IsDynamicCodeSupported && !RuntimeFeature.IsDynamicCodeCompiled;
#else
            return false;
#endif
        }

        private static bool GetIsRunningOnCoreClrInterpreter()
        {
            if (IsCoreCLR)
            {
#if NET
                if (RuntimeFeature.IsDynamicCodeSupported && !RuntimeFeature.IsDynamicCodeCompiled)
                    return true;
#endif
                if (!string.IsNullOrWhiteSpace(GetEnvironmentVariableValue("Interpreter", "")))
                    return true;
                if (int.TryParse(GetEnvironmentVariableValue("InterpMode", "0"), out int mode) && (mode > 0))
                    return true;
            }

            return false;
        }

        private static string GetEnvironmentVariableValue(string envVarName, string defaultValue)
        {
            string str = Environment.GetEnvironmentVariable("DOTNET_" + envVarName) ?? Environment.GetEnvironmentVariable("COMPlus_" + envVarName);
            return str ?? defaultValue;
        }

        private static bool AssemblyConfigurationEquals(string configuration)
        {
            AssemblyConfigurationAttribute assemblyConfigurationAttribute = typeof(string).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();

            return assemblyConfigurationAttribute != null &&
                string.Equals(assemblyConfigurationAttribute.Configuration, configuration, StringComparison.InvariantCulture);
        }
    }
}
