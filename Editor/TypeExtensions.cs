#if USE_MCP

using System;
using System.Linq;
using System.Reflection;

namespace Nurture.MCP.Editor
{
    public static class TypeExtensions
    {
        public static Type FindTypeInCurrentDomain(string fullyQualifiedName)
        {
            // Check dotnet standard first
            var dotNetStandard = AppDomain
                .CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "netstandard");

            var type = dotNetStandard.GetType(fullyQualifiedName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullyQualifiedName);
                if (type != null)
                    return type;
            }
            return null;
        }

        public static Assembly FindAssembly(string name)
        {
            return AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
        }
    }
}

#endif
