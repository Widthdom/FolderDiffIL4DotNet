
.assembly extern System.Runtime
{
  .publickeytoken = ( B0 3F 5F 7F 11 D5 0A 3A ) // .._.....
  .ver 8:0:0:0
}

.assembly extern System.Runtime.InteropServices
{
  .publickeytoken = ( B0 3F 5F 7F 11 D5 0A 3A ) // .._.....
  .ver 8:0:0:0
}

.assembly extern System.Threading
{
  .publickeytoken = ( B0 3F 5F 7F 11 D5 0A 3A ) // .._.....
  .ver 8:0:0:0
}

.assembly 'ILCorpus.Sample'
{
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32) = ( 01 00 08 00 00 00 00 00 ) // ........
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor() = ( 01 00 01 00 54 02 16 57 72 61 70 4E 6F 6E 45 78 63 65 70 74 69 6F 6E 54 68 72 6F 77 73 01 ) // ....T..WrapNonExceptionThrows.
  .custom instance void class [System.Runtime]System.Reflection.AssemblyTitleAttribute::.ctor(string) = ( 01 00 18 6E 69 6C 64 69 66 66 20 49 4C 20 63 6F 72 70 75 73 20 73 61 6D 70 6C 65 00 00 ) // ...nildiff.IL.corpus.sample..
  .custom instance void class [System.Runtime]System.Reflection.AssemblyDescriptionAttribute::.ctor(string) = ( 01 00 3C 54 65 73 74 2D 6F 6E 6C 79 20 61 73 73 65 6D 62 6C 79 20 75 73 65 64 20 74 6F 20 63 61 70 74 75 72 65 20 72 65 61 6C 20 64 69 73 61 73 73 65 6D 62 6C 65 72 20 6F 75 74 70 75 74 2E 00 00 ) // ...Test.only.assembly.used.to.capture.real.disassembler.output...
  .custom instance void class [System.Runtime]System.Reflection.AssemblyFileVersionAttribute::.ctor(string) = ( 01 00 07 31 2E 30 2E 30 2E 30 00 00 ) // ...1.0.0.0..
  .custom instance void class [System.Runtime]System.Runtime.InteropServices.ComVisibleAttribute::.ctor(bool) = ( 01 00 00 00 00 ) // .....
  .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.GuidAttribute::.ctor(string) = ( 01 00 24 64 31 64 38 63 36 63 63 2D 31 64 35 38 2D 34 64 33 62 2D 39 66 32 66 2D 61 30 31 37 32 63 39 63 34 33 33 31 00 00 ) // ...d1d8c6cc.1d58.4d3b.9f2f.a0172c9c4331..
  .custom instance void class [System.Runtime]System.Runtime.Versioning.TargetFrameworkAttribute::.ctor(string) = ( 01 00 18 2E 4E 45 54 43 6F 72 65 41 70 70 2C 56 65 72 73 69 6F 6E 3D 76 38 2E 30 01 00 54 0E 14 46 72 61 6D 65 77 6F 72 6B 44 69 73 70 6C 61 79 4E 61 6D 65 08 2E 4E 45 54 20 38 2E 30 ) // ....NETCoreApp.Version.v8.0..T..FrameworkDisplayName..NET.8.0
  .hash algorithm 0x00008004
  .ver 1:0:0:0
}

.module 'ILCorpus.Sample.dll'
// MVID: {06e8a859-d81d-48da-a59a-eb5b8dc94c2e}
.imagebase 0x10000000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003  // WindowsCui
.corflags 0x00000009  // 

.class public auto ansi sealed ILCorpus.Sample.RepresentativeType`1 extends [System.Runtime]System.Object
{
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(byte) = ( 01 00 01 00 00 ) // .....
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(byte) = ( 01 00 00 00 00 ) // .....
  .field public static int32 DefaultValue
  .field public static initonly string StaticValue
  .field private var _value
  .field private string '<Name>k__BackingField'
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
  .field private class [System.Runtime]System.EventHandler Changed
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(byte) = ( 01 00 02 00 00 ) // .....
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
  .method private hidebysig specialname rtspecialname static default void .cctor() cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x2050
    // Code size 11 (0xB)
    .maxstack 8
    IL_0000: ldstr "initialized"
    IL_0005: stsfld string [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::StaticValue
    IL_000a: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::.cctor()
  .method public hidebysig specialname rtspecialname instance default void .ctor() cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x205C
    // Code size 29 (0x1D)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: call instance void class [System.Runtime]System.Object::.ctor()
    IL_0006: ldarg.0
    IL_0007: call !!0 class [System.Runtime]System.Activator::CreateInstance<var>()
    IL_000c: stfld var [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::_value
    IL_0011: ldarg.0
    IL_0012: ldsfld string string::Empty
    IL_0017: call instance void class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::set_Name(string)
    IL_001c: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::.ctor()
  .method public hidebysig specialname instance default string get_Name() cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x207A
    // Code size 7 (0x7)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldfld string [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::'<Name>k__BackingField'
    IL_0006: ret
  } // End of method System.String ILCorpus.Sample.RepresentativeType`1::get_Name()
  .method public hidebysig specialname instance default void set_Name(string 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x2082
    // Code size 8 (0x8)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldarg.1
    IL_0002: stfld string [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::'<Name>k__BackingField'
    IL_0007: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::set_Name(System.String)
  .method public hidebysig specialname instance default var get_Value() cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x208B
    // Code size 7 (0x7)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldfld var [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::_value
    IL_0006: ret
  } // End of method T ILCorpus.Sample.RepresentativeType`1::get_Value()
  .method public hidebysig specialname instance default void set_Value(var 'value') cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x2093
    // Code size 30 (0x1E)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldarg.1
    IL_0002: stfld var [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::_value
    IL_0007: ldarg.0
    IL_0008: ldfld class [System.Runtime]System.EventHandler [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::Changed
    IL_000d: dup
    IL_000e: brtrue.s     IL_0012
    IL_0010: pop
    IL_0011: ret
    IL_0012: ldarg.0
    IL_0013: ldsfld class [System.Runtime]System.EventArgs [System.Runtime]System.EventArgs::Empty
    IL_0018: callvirt instance void class [System.Runtime]System.EventHandler::Invoke([System.Runtime]System.Object, [System.Runtime]System.EventArgs)
    IL_001d: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::set_Value(T)
  .method public hidebysig specialname instance default void add_Changed(class [System.Runtime]System.EventHandler 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(byte) = ( 01 00 02 00 00 ) // .....
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x20B4
    // Code size 41 (0x29)
    .maxstack 3
    .locals init(class [System.Runtime]System.EventHandler V_0, class [System.Runtime]System.EventHandler V_1, class [System.Runtime]System.EventHandler V_2)
    IL_0000: ldarg.0
    IL_0001: ldfld class [System.Runtime]System.EventHandler [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::Changed
    IL_0006: stloc.0
    IL_0007: ldloc.0
    IL_0008: stloc.1
    IL_0009: ldloc.1
    IL_000a: ldarg.1
    IL_000b: call [System.Runtime]System.Delegate class [System.Runtime]System.Delegate::Combine([System.Runtime]System.Delegate, [System.Runtime]System.Delegate)
    IL_0010: castclass class System.EventHandler
    IL_0015: stloc.2
    IL_0016: ldarg.0
    IL_0017: ldflda class [System.Runtime]System.EventHandler [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::Changed
    IL_001c: ldloc.2
    IL_001d: ldloc.1
    IL_001e: call !!0 class [System.Threading]System.Threading.Interlocked::CompareExchange<class [System.Runtime]System.EventHandler>(!!0&, !!0, !!0)
    IL_0023: stloc.0
    IL_0024: ldloc.0
    IL_0025: ldloc.1
    IL_0026: bne.un.s     IL_0007
    IL_0028: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::add_Changed(System.EventHandler)
  .method public hidebysig specialname instance default void remove_Changed(class [System.Runtime]System.EventHandler 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(byte) = ( 01 00 02 00 00 ) // .....
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x20EC
    // Code size 41 (0x29)
    .maxstack 3
    .locals init(class [System.Runtime]System.EventHandler V_0, class [System.Runtime]System.EventHandler V_1, class [System.Runtime]System.EventHandler V_2)
    IL_0000: ldarg.0
    IL_0001: ldfld class [System.Runtime]System.EventHandler [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::Changed
    IL_0006: stloc.0
    IL_0007: ldloc.0
    IL_0008: stloc.1
    IL_0009: ldloc.1
    IL_000a: ldarg.1
    IL_000b: call [System.Runtime]System.Delegate class [System.Runtime]System.Delegate::Remove([System.Runtime]System.Delegate, [System.Runtime]System.Delegate)
    IL_0010: castclass class System.EventHandler
    IL_0015: stloc.2
    IL_0016: ldarg.0
    IL_0017: ldflda class [System.Runtime]System.EventHandler [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::Changed
    IL_001c: ldloc.2
    IL_001d: ldloc.1
    IL_001e: call !!0 class [System.Threading]System.Threading.Interlocked::CompareExchange<class [System.Runtime]System.EventHandler>(!!0&, !!0, !!0)
    IL_0023: stloc.0
    IL_0024: ldloc.0
    IL_0025: ldloc.1
    IL_0026: bne.un.s     IL_0007
    IL_0028: ret
  } // End of method System.Void ILCorpus.Sample.RepresentativeType`1::remove_Changed(System.EventHandler)
  .method public hidebysig instance default int32 Add(int32 left, int32 right) cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x2121
    // Code size 4 (0x4)
    .maxstack 8
    IL_0000: ldarg.1
    IL_0001: ldarg.2
    IL_0002: add
    IL_0003: ret
  } // End of method System.Int32 ILCorpus.Sample.RepresentativeType`1::Add(System.Int32,System.Int32)
  .method public hidebysig instance default mvar Convert(class [System.Runtime]System.Func`2<T,TResult> converter) cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x2126
    // Code size 13 (0xD)
    .maxstack 8
    IL_0000: ldarg.1
    IL_0001: ldarg.0
    IL_0002: ldfld var [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1<T>::_value
    IL_0007: callvirt instance var class [System.Runtime]System.Func`2<T,TResult>::Invoke(var)
    IL_000c: ret
  } // End of method TResult ILCorpus.Sample.RepresentativeType`1::Convert(System.Func`2<T,TResult>)
  .method public hidebysig instance default [System.Runtime]System.Threading.Tasks.Task`1<System.Int32> ComputeAsync(int32 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.AsyncStateMachineAttribute::.ctor([System.Runtime]System.Type) = ( 01 00 38 49 4C 43 6F 72 70 75 73 2E 53 61 6D 70 6C 65 2E 52 65 70 72 65 73 65 6E 74 61 74 69 76 65 54 79 70 65 60 31 2B 3C 43 6F 6D 70 75 74 65 41 73 79 6E 63 3E 64 5F 5F 31 37 00 00 ) // ..8ILCorpus.Sample.RepresentativeType.1..ComputeAsync.d__17..
    // Method begins at Relative Virtual Address (RVA) 0x2134
    // Code size 55 (0x37)
    .maxstack 2
    .locals init(class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T> V_0)
    IL_0000: ldloca.s class V_0
    IL_0002: call [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::Create()
    IL_0007: stfld class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
    IL_000c: ldloca.s class V_0
    IL_000e: ldarg.1
    IL_000f: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::value
    IL_0014: ldloca.s class V_0
    IL_0016: ldc.i4.m1
    IL_0017: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
    IL_001c: ldloca.s class V_0
    IL_001e: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
    IL_0023: ldloca.s class V_0
    IL_0025: call instance System.Void class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::Start<    ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<var>>(!!0&)
    IL_002a: ldloca.s class V_0
    IL_002c: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
    IL_0031: call instance [System.Runtime]System.Threading.Tasks.Task`1<!0> class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::get_Task()
    IL_0036: ret
  } // End of method System.Threading.Tasks.Task`1<System.Int32> ILCorpus.Sample.RepresentativeType`1::ComputeAsync(System.Int32)
  .method public hidebysig instance default [System.Runtime]System.Collections.Generic.IEnumerable`1<System.Int32> CountTo(int32 maximum) cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor([System.Runtime]System.Type) = ( 01 00 33 49 4C 43 6F 72 70 75 73 2E 53 61 6D 70 6C 65 2E 52 65 70 72 65 73 65 6E 74 61 74 69 76 65 54 79 70 65 60 31 2B 3C 43 6F 75 6E 74 54 6F 3E 64 5F 5F 31 38 00 00 ) // ..3ILCorpus.Sample.RepresentativeType.1..CountTo.d__18..
    // Method begins at Relative Virtual Address (RVA) 0x2177
    // Code size 15 (0xF)
    .maxstack 8
    IL_0000: ldc.i4.s -2
    IL_0002: newobj instance void class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::.ctor(int32)
    IL_0007: dup
    IL_0008: ldarg.1
    IL_0009: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>3__maximum'
    IL_000e: ret
  } // End of method System.Collections.Generic.IEnumerable`1<System.Int32> ILCorpus.Sample.RepresentativeType`1::CountTo(System.Int32)
  .method public hidebysig instance default [System.Runtime]System.Func`2<System.Int32,System.Int32> CreateAdder(int32 offset) cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x2187
    // Code size 24 (0x18)
    .maxstack 8
    IL_0000: newobj instance void class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0<T>::.ctor()
    IL_0005: dup
    IL_0006: ldarg.1
    IL_0007: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0<T>::offset
    IL_000c: ldftn instance int32 class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0<T>::<CreateAdder>b__0(int32)
    IL_0012: newobj instance void class [System.Runtime]System.Func`2<System.Int32,System.Int32>::.ctor([System.Runtime]System.Object, [System.Runtime]System.IntPtr)
    IL_0017: ret
  } // End of method System.Func`2<System.Int32,System.Int32> ILCorpus.Sample.RepresentativeType`1::CreateAdder(System.Int32)
  .property instance string Name ()
  {
    .get instance default string ILCorpus.Sample.RepresentativeType`1::get_Name ()
    .set instance default void ILCorpus.Sample.RepresentativeType`1::set_Name (string 'value')
  } // End of property System.String ILCorpus.Sample.RepresentativeType`1::Name()
  .property instance var Value ()
  {
    .get instance default var ILCorpus.Sample.RepresentativeType`1::get_Value ()
    .set instance default void ILCorpus.Sample.RepresentativeType`1::set_Value (var 'value')
  } // End of property T ILCorpus.Sample.RepresentativeType`1::Value()
  .event class [System.Runtime]System.EventHandler Changed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(byte) = ( 01 00 02 00 00 ) // .....
    .addon instance default void ILCorpus.Sample.RepresentativeType`1::add_Changed (class [System.Runtime]System.EventHandler 'value')
    .removeon instance default void ILCorpus.Sample.RepresentativeType`1::remove_Changed (class [System.Runtime]System.EventHandler 'value')
  } // End of property System.EventHandler ILCorpus.Sample.RepresentativeType`1::Changed

  .class nested private auto ansi sealed beforefieldinit <>c__DisplayClass19_0 extends [System.Runtime]System.Object
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    .field public int32 offset
    .method public hidebysig specialname rtspecialname instance default void .ctor() cil managed
    {
      // Method begins at Relative Virtual Address (RVA) 0x21BC
      // Code size 7 (0x7)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: call instance void class [System.Runtime]System.Object::.ctor()
      IL_0006: ret
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0::.ctor()
    .method hidebysig instance default int32 <CreateAdder>b__0(int32 'value') cil managed
    {
      // Method begins at Relative Virtual Address (RVA) 0x21C4
      // Code size 9 (0x9)
      .maxstack 8
      IL_0000: ldarg.1
      IL_0001: ldarg.0
      IL_0002: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0<T>::offset
      IL_0007: add
      IL_0008: ret
    } // End of method System.Int32 ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0::<CreateAdder>b__0(System.Int32)
  } // End of class ILCorpus.Sample.RepresentativeType`1/<>c__DisplayClass19_0

  .class nested private auto ansi sealed beforefieldinit <ComputeAsync>d__17 extends [System.Runtime]System.ValueType implements [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    .field public int32 '<>1__state'
    .field public class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> '<>t__builder'
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(byte) = ( 01 00 00 00 00 ) // .....
    .field public int32 value
    .field private valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter '<>u__1'
    .method private hidebysig newslot virtual final instance default void MoveNext() cil managed
    {
      // Method begins at Relative Virtual Address (RVA) 0x21D0
      // Code size 157 (0x9D)
      .maxstack 3
      .locals init(int32 V_0, int32 V_1, valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter V_2, valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable V_3, class [System.Runtime]System.Exception V_4)
      IL_0000: ldarg.0
      IL_0001: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
      IL_0006: stloc.0
      .try
      {
        IL_0007: ldloc.0
        IL_0008: brfalse.s         IL_0041
        IL_000a: call valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable class [System.Runtime]System.Threading.Tasks.Task::Yield()
        IL_000f: stloc.3
        IL_0010: ldloca.s class V_3
        IL_0012: call instance valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter class valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable::GetAwaiter()
        IL_0017: stloc.2
        IL_0018: ldloca.s class V_2
        IL_001a: call instance bool class valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::get_IsCompleted()
        IL_001f: brtrue.s         IL_005d
        IL_0021: ldarg.0
        IL_0022: ldc.i4.0
        IL_0023: dup
        IL_0024: stloc.0
        IL_0025: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
        IL_002a: ldarg.0
        IL_002b: ldloc.2
        IL_002c: stfld valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>u__1'
        IL_0031: ldarg.0
        IL_0032: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
        IL_0037: ldloca.s class V_2
        IL_0039: ldarg.0
        IL_003a: call instance System.Void class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::AwaitUnsafeOnCompleted<valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter        ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<var>>(!!0&, !!1&)
        IL_003f: leave.s         IL_009c
        IL_0041: ldarg.0
        IL_0042: ldfld valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>u__1'
        IL_0047: stloc.2
        IL_0048: ldarg.0
        IL_0049: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>u__1'
        IL_004e: initobj class System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter
        IL_0054: ldarg.0
        IL_0055: ldc.i4.m1
        IL_0056: dup
        IL_0057: stloc.0
        IL_0058: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
        IL_005d: ldloca.s class V_2
        IL_005f: call instance void class valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::GetResult()
        IL_0064: ldarg.0
        IL_0065: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::value
        IL_006a: ldc.i4.7
        IL_006b: add
        IL_006c: stloc.1
        IL_006d: leave.s         IL_0088
      }
      catch [System.Runtime]System.Exception
      {
        IL_006f: stloc.s class V_4
        IL_0071: ldarg.0
        IL_0072: ldc.i4.s -2
        IL_0074: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
        IL_0079: ldarg.0
        IL_007a: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
        IL_007f: ldloc.s class V_4
        IL_0081: call instance void class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::SetException([System.Runtime]System.Exception)
        IL_0086: leave.s         IL_009c
      }
      IL_0088: ldarg.0
      IL_0089: ldc.i4.s -2
      IL_008b: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>1__state'
      IL_0090: ldarg.0
      IL_0091: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
      IL_0096: ldloc.1
      IL_0097: call instance void class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::SetResult(var)
      IL_009c: ret
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17::MoveNext()
    .method private hidebysig newslot virtual final instance default void SetStateMachine(class [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine stateMachine) cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x228C
      // Code size 13 (0xD)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: ldflda class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32> [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17<T>::'<>t__builder'
      IL_0006: ldarg.1
      IL_0007: call instance void class [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<System.Int32>::SetStateMachine([System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine)
      IL_000c: ret
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17::SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine)
  } // End of class ILCorpus.Sample.RepresentativeType`1/<ComputeAsync>d__17

  .class nested private auto ansi sealed beforefieldinit <CountTo>d__18 extends [System.Runtime]System.Object implements [System.Runtime]System.Collections.Generic.IEnumerable`1<System.Int32>, [System.Runtime]System.Collections.IEnumerable, [System.Runtime]System.Collections.Generic.IEnumerator`1<System.Int32>, [System.Runtime]System.Collections.IEnumerator, [System.Runtime]System.IDisposable
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    .field private int32 '<>1__state'
    .field private int32 '<>2__current'
    .field private int32 '<>l__initialThreadId'
    .field private int32 maximum
    .field public int32 '<>3__maximum'
    .field private int32 '<value>5__2'
    .method public hidebysig specialname rtspecialname instance default void .ctor(int32 <>1__state) cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x229A
      // Code size 25 (0x19)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: call instance void class [System.Runtime]System.Object::.ctor()
      IL_0006: ldarg.0
      IL_0007: ldarg.1
      IL_0008: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_000d: ldarg.0
      IL_000e: call int32 class [System.Runtime]System.Environment::get_CurrentManagedThreadId()
      IL_0013: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>l__initialThreadId'
      IL_0018: ret
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::.ctor(System.Int32)
    .method private hidebysig newslot virtual final instance default void System.IDisposable.Dispose() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x22B4
      // Code size 1 (0x1)
      .maxstack 8
      IL_0000: ret
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.IDisposable.Dispose()
    .method private hidebysig newslot virtual final instance default bool MoveNext() cil managed
    {
      // Method begins at Relative Virtual Address (RVA) 0x22B8
      // Code size 92 (0x5C)
      .maxstack 3
      .locals init(int32 V_0, int32 V_1)
      IL_0000: ldarg.0
      IL_0001: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_0006: stloc.0
      IL_0007: ldloc.0
      IL_0008: brfalse.s       IL_0010
      IL_000a: ldloc.0
      IL_000b: ldc.i4.1
      IL_000c: beq.s       IL_0035
      IL_000e: ldc.i4.0
      IL_000f: ret
      IL_0010: ldarg.0
      IL_0011: ldc.i4.m1
      IL_0012: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_0017: ldarg.0
      IL_0018: ldc.i4.0
      IL_0019: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<value>5__2'
      IL_001e: br.s       IL_004c
      IL_0020: ldarg.0
      IL_0021: ldarg.0
      IL_0022: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<value>5__2'
      IL_0027: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>2__current'
      IL_002c: ldarg.0
      IL_002d: ldc.i4.1
      IL_002e: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_0033: ldc.i4.1
      IL_0034: ret
      IL_0035: ldarg.0
      IL_0036: ldc.i4.m1
      IL_0037: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_003c: ldarg.0
      IL_003d: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<value>5__2'
      IL_0042: stloc.1
      IL_0043: ldarg.0
      IL_0044: ldloc.1
      IL_0045: ldc.i4.1
      IL_0046: add
      IL_0047: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<value>5__2'
      IL_004c: ldarg.0
      IL_004d: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<value>5__2'
      IL_0052: ldarg.0
      IL_0053: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::maximum
      IL_0058: blt.s       IL_0020
      IL_005a: ldc.i4.0
      IL_005b: ret
    } // End of method System.Boolean ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::MoveNext()
    .method private hidebysig newslot virtual specialname final instance default int32 System.Collections.Generic.IEnumerator<System.Int32>.get_Current() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x2320
      // Code size 7 (0x7)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>2__current'
      IL_0006: ret
    } // End of method System.Int32 ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.Generic.IEnumerator<System.Int32>.get_Current()
    .method private hidebysig newslot virtual final instance default void System.Collections.IEnumerator.Reset() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x2328
      // Code size 6 (0x6)
      .maxstack 8
      IL_0000: newobj instance void class [System.Runtime]System.NotSupportedException::.ctor()
      IL_0005: throw
    } // End of method System.Void ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.IEnumerator.Reset()
    .method private hidebysig newslot virtual specialname final instance default [System.Runtime]System.Object System.Collections.IEnumerator.get_Current() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      .param [0]
      .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(byte) = ( 01 00 00 00 00 ) // .....
      // Method begins at Relative Virtual Address (RVA) 0x232F
      // Code size 12 (0xC)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>2__current'
      IL_0006: box class System.Int32
      IL_000b: ret
    } // End of method System.Object ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.IEnumerator.get_Current()
    .method private hidebysig newslot virtual final instance default [System.Runtime]System.Collections.Generic.IEnumerator`1<System.Int32> System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x233C
      // Code size 55 (0x37)
      .maxstack 2
      .locals init(class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T> V_0)
      IL_0000: ldarg.0
      IL_0001: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_0006: ldc.i4.s -2
      IL_0008: bne.un.s       IL_0022
      IL_000a: ldarg.0
      IL_000b: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>l__initialThreadId'
      IL_0010: call int32 class [System.Runtime]System.Environment::get_CurrentManagedThreadId()
      IL_0015: bne.un.s       IL_0022
      IL_0017: ldarg.0
      IL_0018: ldc.i4.0
      IL_0019: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>1__state'
      IL_001e: ldarg.0
      IL_001f: stloc.0
      IL_0020: br.s       IL_0029
      IL_0022: ldc.i4.0
      IL_0023: newobj instance void class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::.ctor(int32)
      IL_0028: stloc.0
      IL_0029: ldloc.0
      IL_002a: ldarg.0
      IL_002b: ldfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::'<>3__maximum'
      IL_0030: stfld int32 [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::maximum
      IL_0035: ldloc.0
      IL_0036: ret
    } // End of method System.Collections.Generic.IEnumerator`1<System.Int32> ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator()
    .method private hidebysig newslot virtual final instance default [System.Runtime]System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() cil managed
    {
      .custom instance void class [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = ( 01 00 00 00 ) // ....
      // Method begins at Relative Virtual Address (RVA) 0x237F
      // Code size 7 (0x7)
      .maxstack 8
      IL_0000: ldarg.0
      IL_0001: call instance [System.Runtime]System.Collections.Generic.IEnumerator`1<System.Int32> class [ILCorpus.Sample.dll]ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18<T>::System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator()
      IL_0006: ret
    } // End of method System.Collections.IEnumerator ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.IEnumerable.GetEnumerator()
    .property instance int32 System.Collections.Generic.IEnumerator<System.Int32>.Current ()
    {
      .get instance default int32 ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.Generic.IEnumerator<System.Int32>.get_Current ()
    } // End of property System.Int32 ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.Generic.IEnumerator<System.Int32>.Current()
    .property instance [System.Runtime]System.Object System.Collections.IEnumerator.Current ()
    {
      .get instance default [System.Runtime]System.Object ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.IEnumerator.get_Current ()
    } // End of property System.Object ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18::System.Collections.IEnumerator.Current()
  } // End of class ILCorpus.Sample.RepresentativeType`1/<CountTo>d__18
} // End of class ILCorpus.Sample.RepresentativeType`1

.class public interface abstract auto ansi beforefieldinit ILCorpus.Sample.ICorpusComContract
{
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(byte) = ( 01 00 01 00 00 ) // .....
  .custom instance void class [System.Runtime]System.Runtime.InteropServices.ComVisibleAttribute::.ctor(bool) = ( 01 00 01 00 00 ) // .....
  .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.GuidAttribute::.ctor(string) = ( 01 00 24 35 38 38 38 33 30 34 35 2D 39 32 62 34 2D 34 63 39 66 2D 61 34 32 61 2D 33 65 32 35 61 39 62 39 30 66 37 38 00 00 ) // ...58883045.92b4.4c9f.a42a.3e25a9b90f78..
  .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.InterfaceTypeAttribute::.ctor(valuetype [System.Runtime.InteropServices]System.Runtime.InteropServices.ComInterfaceType) = ( 01 00 00 00 00 00 00 00 ) // ........
  .method public hidebysig newslot abstract virtual instance default string Echo(string 'value') cil managed
  {
    .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.DispIdAttribute::.ctor(int32) = ( 01 00 01 00 00 00 00 00 ) // ........
  } // End of method System.String ILCorpus.Sample.ICorpusComContract::Echo(System.String)
  .method public hidebysig newslot abstract virtual specialname instance default int32 get_Number() cil managed
  {
  } // End of method System.Int32 ILCorpus.Sample.ICorpusComContract::get_Number()
  .method public hidebysig newslot abstract virtual specialname instance default void set_Number(int32 'value') cil managed
  {
  } // End of method System.Void ILCorpus.Sample.ICorpusComContract::set_Number(System.Int32)
  .property instance int32 Number ()
  {
    .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.DispIdAttribute::.ctor(int32) = ( 01 00 02 00 00 00 00 00 ) // ........
    .get instance default int32 ILCorpus.Sample.ICorpusComContract::get_Number ()
    .set instance default void ILCorpus.Sample.ICorpusComContract::set_Number (int32 'value')
  } // End of property System.Int32 ILCorpus.Sample.ICorpusComContract::Number()
} // End of class ILCorpus.Sample.ICorpusComContract

.class public auto ansi sealed beforefieldinit ILCorpus.Sample.CorpusComClass extends [System.Runtime]System.Object implements ILCorpus.Sample.ICorpusComContract
{
  .custom instance void class [System.Runtime]System.Runtime.InteropServices.ComVisibleAttribute::.ctor(bool) = ( 01 00 01 00 00 ) // .....
  .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.GuidAttribute::.ctor(string) = ( 01 00 24 34 33 32 33 39 38 65 31 2D 33 33 34 66 2D 34 35 61 66 2D 38 33 38 37 2D 38 32 66 33 34 62 38 61 63 35 32 36 00 00 ) // ...432398e1.334f.45af.8387.82f34b8ac526..
  .custom instance void class [System.Runtime.InteropServices]System.Runtime.InteropServices.ClassInterfaceAttribute::.ctor(valuetype [System.Runtime.InteropServices]System.Runtime.InteropServices.ClassInterfaceType) = ( 01 00 00 00 00 00 00 00 ) // ........
  .field private int32 '<Number>k__BackingField'
  .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
  .method public hidebysig newslot virtual specialname final instance default int32 get_Number() cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x21A0
    // Code size 7 (0x7)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldfld int32 ILCorpus.Sample.CorpusComClass::'<Number>k__BackingField'
    IL_0006: ret
  } // End of method System.Int32 ILCorpus.Sample.CorpusComClass::get_Number()
  .method public hidebysig newslot virtual specialname final instance default void set_Number(int32 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    // Method begins at Relative Virtual Address (RVA) 0x21A8
    // Code size 8 (0x8)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: ldarg.1
    IL_0002: stfld int32 ILCorpus.Sample.CorpusComClass::'<Number>k__BackingField'
    IL_0007: ret
  } // End of method System.Void ILCorpus.Sample.CorpusComClass::set_Number(System.Int32)
  .method public hidebysig newslot virtual final instance default string Echo(string 'value') cil managed
  {
    .custom instance void class [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(byte) = ( 01 00 01 00 00 ) // .....
    // Method begins at Relative Virtual Address (RVA) 0x21B1
    // Code size 2 (0x2)
    .maxstack 8
    IL_0000: ldarg.1
    IL_0001: ret
  } // End of method System.String ILCorpus.Sample.CorpusComClass::Echo(System.String)
  .method public hidebysig specialname rtspecialname instance default void .ctor() cil managed
  {
    // Method begins at Relative Virtual Address (RVA) 0x21B4
    // Code size 7 (0x7)
    .maxstack 8
    IL_0000: ldarg.0
    IL_0001: call instance void class [System.Runtime]System.Object::.ctor()
    IL_0006: ret
  } // End of method System.Void ILCorpus.Sample.CorpusComClass::.ctor()
  .property instance int32 Number ()
  {
    .get instance default int32 ILCorpus.Sample.CorpusComClass::get_Number ()
    .set instance default void ILCorpus.Sample.CorpusComClass::set_Number (int32 'value')
  } // End of property System.Int32 ILCorpus.Sample.CorpusComClass::Number()
} // End of class ILCorpus.Sample.CorpusComClass
