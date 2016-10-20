using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Generator
{
    public static class ProxyGenerator
    {
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
    }
}
