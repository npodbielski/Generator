using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;

namespace Generator
{
    public static class ProxyGenerator
    {
        public static TReturn BaseServiceCall<TParam, TReturn>(string url, string method, TParam param)
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var serializeObject = JsonConvert.SerializeObject(param);
                var stringContent = new StringContent(serializeObject, Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> call;
                if (method == "POST")
                {
                    call = client.PostAsync(url, stringContent);
                }
                else
                {
                    call = client.GetAsync(url + param);
                }
                var result = call.Result.Content;
                return (TReturn)JsonConvert.DeserializeObject(result.ReadAsStringAsync().Result, typeof(TReturn));
            }
        }

        public static T PropertyChangedProxy<T>() where T : class, new()
        {
            var type = typeof(T);
            var assemblyName = type.FullName + "_Proxy";
            var fileName = assemblyName + ".dll";
            var name = new AssemblyName(assemblyName);
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(assemblyName, fileName);
            var typeBuilder = module.DefineType(type.Name + "Proxy",
                TypeAttributes.Class | TypeAttributes.Public, type);
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var raiseEventMethod = ImplementPropertyChanged(typeBuilder);
            var propertyInfos = type.GetProperties().Where(p => p.CanRead && p.CanWrite);
            foreach (var item in propertyInfos)
            {
                var baseMethod = item.GetGetMethod();
                var getAccessor = typeBuilder.DefineMethod(baseMethod.Name, baseMethod.Attributes, item.PropertyType, null);
                var il = getAccessor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Call, baseMethod, null);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(getAccessor, baseMethod);
                baseMethod = item.GetSetMethod();
                var setAccessor = typeBuilder.DefineMethod(baseMethod.Name, baseMethod.Attributes, typeof(void), new[] { item.PropertyType });
                il = setAccessor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, baseMethod);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, item.Name);
                il.Emit(OpCodes.Callvirt, raiseEventMethod);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(setAccessor, baseMethod);
            }
            var t = typeBuilder.CreateType();
            assembly.Save(fileName);
            return Activator.CreateInstance(t) as T;
        }

        public static T ServiceProxy<T>(string baseUrl) where T : class
        {
            var serviceInterface = typeof(T);
            if (serviceInterface.IsInterface)
            {
                var assemblyName = serviceInterface.FullName + "_Proxy";
                var fileName = assemblyName + ".dll";
                var name = new AssemblyName(assemblyName);
                var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
                var module = assembly.DefineDynamicModule(assemblyName, fileName);
                var implemntationName = serviceInterface.Name.StartsWith("I") ?
                    serviceInterface.Name.Substring(1) : serviceInterface.Name;
                var typeBuilder = module.DefineType(implemntationName + "Proxy",
                    TypeAttributes.Class | TypeAttributes.Public);
                typeBuilder.AddInterfaceImplementation(serviceInterface);
                foreach (var method in serviceInterface.GetMethods().Where(m => !m.IsSpecialName))
                {
                    var customAttributes = method.GetCustomAttributes<OperationContractAttribute>()
                        .SingleOrDefault();
                    if (customAttributes != null)
                    {
                        var webInvokeAttr = method.GetCustomAttribute<WebInvokeAttribute>();
                        var webGetAttr = method.GetCustomAttribute<WebGetAttribute>();
                        ImplementServiceMethod(baseUrl, typeBuilder, method, webInvokeAttr, webGetAttr);
                    }
                    else
                    {
                        throw new Exception("Service interface has to be marked with correct method attribute!");
                    }
                }
                var type = typeBuilder.CreateType();
                assembly.Save(assemblyName);
                return (T)Activator.CreateInstance(type);
            }
            return null;
        }

        private static void ImplementAddEvent(TypeBuilder typeBuilder, FieldBuilder field, EventBuilder eventInfo)
        {
            var ibaseMethod = typeof(INotifyPropertyChanged).GetMethod("add_PropertyChanged");
            var addMethod = typeBuilder.DefineMethod("add_PropertyChanged",
                ibaseMethod.Attributes ^ MethodAttributes.Abstract,
                ibaseMethod.CallingConvention,
                ibaseMethod.ReturnType,
                new[] { typeof(PropertyChangedEventHandler) });
            var generator = addMethod.GetILGenerator();
            var combine = typeof(Delegate).GetMethod("Combine", new[] { typeof(Delegate), typeof(Delegate) });
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, combine);
            generator.Emit(OpCodes.Castclass, typeof(PropertyChangedEventHandler));
            generator.Emit(OpCodes.Stfld, field);
            generator.Emit(OpCodes.Ret);
            eventInfo.SetAddOnMethod(addMethod);
        }

        private static MethodBuilder ImplementOnPropertyChanged(TypeBuilder typeBuilder, FieldBuilder field,
            EventBuilder eventInfo)
        {
            var methodBuilder = typeBuilder.DefineMethod("OnPropertyChanged",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig |
                MethodAttributes.NewSlot, typeof(void),
                new[] { typeof(string) });
            var generator = methodBuilder.GetILGenerator();
            var returnLabel = generator.DefineLabel();
            var propertyArgsCtor = typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) });
            generator.DeclareLocal(typeof(PropertyChangedEventHandler));
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Stloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Brfalse, returnLabel);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Newobj, propertyArgsCtor);
            generator.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke"));
            generator.MarkLabel(returnLabel);
            generator.Emit(OpCodes.Ret);
            eventInfo.SetRaiseMethod(methodBuilder);
            return methodBuilder;
        }

        private static MethodBuilder ImplementOnPropertyChangedHelper(TypeBuilder typeBuilder, FieldBuilder field,
            EventBuilder eventInfo)
        {
            var methodBuilder = typeBuilder.DefineMethod("OnPropertyChanged",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig |
                MethodAttributes.NewSlot, CallingConventions.Standard | CallingConventions.HasThis, typeof(void),
                new[] { typeof(string) });
            var generator = methodBuilder.GetILGenerator();
            var returnLabel = generator.DefineLabel();
            generator.DeclareLocal(typeof(PropertyChangedEventHandler));
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, typeof(PropertyChangedInvoker).GetMethod("Invoke"));
            generator.MarkLabel(returnLabel);
            generator.Emit(OpCodes.Ret);
            eventInfo.SetRaiseMethod(methodBuilder);
            return methodBuilder;
        }

        private static MethodBuilder ImplementPropertyChanged(TypeBuilder typeBuilder)
        {
            typeBuilder.AddInterfaceImplementation(typeof(INotifyPropertyChanged));
            var field = typeBuilder.DefineField("PropertyChanged", typeof(PropertyChangedEventHandler), FieldAttributes.Private);
            var eventInfo = typeBuilder.DefineEvent("PropertyChanged", EventAttributes.None, typeof(PropertyChangedEventHandler));
            //var methodBuilder = ImplementOnPropertyChangedHelper(typeBuilder, field, eventInfo);
            var methodBuilder = ImplementOnPropertyChanged(typeBuilder, field, eventInfo);
            ImplementAddEvent(typeBuilder, field, eventInfo);
            ImplementRemoveEvent(typeBuilder, field, eventInfo);
            return methodBuilder;
        }

        private static void ImplementRemoveEvent(TypeBuilder typeBuilder, FieldBuilder field, EventBuilder eventInfo)
        {
            var ibaseMethod = typeof(INotifyPropertyChanged).GetMethod("remove_PropertyChanged");
            var removeMethod = typeBuilder.DefineMethod("remove_PropertyChanged",
                ibaseMethod.Attributes ^ MethodAttributes.Abstract,
                ibaseMethod.CallingConvention,
                ibaseMethod.ReturnType,
                new[] { typeof(PropertyChangedEventHandler) });
            var remove = typeof(Delegate).GetMethod("Remove", new[] { typeof(Delegate), typeof(Delegate) });
            var generator = removeMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, remove);
            generator.Emit(OpCodes.Castclass, typeof(PropertyChangedEventHandler));
            generator.Emit(OpCodes.Stfld, field);
            generator.Emit(OpCodes.Ret);
            eventInfo.SetRemoveOnMethod(removeMethod);
        }

        private static void ImplementServiceMethod(string baseUrl, TypeBuilder typeBuilder, MethodInfo method,
                                                    WebInvokeAttribute webInvokeAttr, WebGetAttribute webGetAttr)
        {
            var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            var methodBuilder = typeBuilder.DefineMethod(method.Name, method.Attributes ^ MethodAttributes.Abstract,
                 method.CallingConvention, method.ReturnType,
                 parameterTypes);
            var il = methodBuilder.GetILGenerator();
            var serviceCallMethod = typeof(ProxyGenerator).GetMethod("BaseServiceCall",
                BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(parameterTypes[0], method.ReturnType);

            var url = new Uri(new Uri(baseUrl), method.Name).AbsoluteUri;
            if (webGetAttr != null)
            {
                url = url + "?" + method.GetParameters()[0].Name + "=";
            }

            il.Emit(OpCodes.Ldstr, url);
            il.Emit(OpCodes.Ldstr, webGetAttr != null ? "GET" : "POST");
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, serviceCallMethod);
            il.Emit(OpCodes.Ret);
        }
    }
}