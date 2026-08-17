// IL code: ILCorpus.Sample
.class private auto ansi '<Module>'
{
} // end of class <Module>

.class public auto ansi sealed ILCorpus.Sample.RepresentativeType`1<class .ctor T>
	extends [System.Runtime]System.Object
{
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = (
		01 00 01 00 00
	)
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
		01 00 00 00 00
	)
	// Nested Types
	.class nested private auto ansi sealed beforefieldinit '<>c__DisplayClass19_0'<class .ctor T>
		extends [System.Runtime]System.Object
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field public int32 offset

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor () cil managed 
		{
			// Method begins at RVA 0x21bc
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [System.Runtime]System.Object::.ctor()
			IL_0006: ret
		} // end of method '<>c__DisplayClass19_0'::.ctor

		.method assembly hidebysig 
			instance int32 '<CreateAdder>b__0' (
				int32 'value'
			) cil managed 
		{
			// Method begins at RVA 0x21c4
			// Header size: 1
			// Code size: 9 (0x9)
			.maxstack 8

			IL_0000: ldarg.1
			IL_0001: ldarg.0
			IL_0002: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<>c__DisplayClass19_0'<!T>::offset
			IL_0007: add
			IL_0008: ret
		} // end of method '<>c__DisplayClass19_0'::'<CreateAdder>b__0'

	} // end of class <>c__DisplayClass19_0

	.class nested private auto ansi sealed beforefieldinit '<ComputeAsync>d__17'<class .ctor T>
		extends [System.Runtime]System.ValueType
		implements [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		.interfaceimpl type [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		// Fields
		.field public int32 '<>1__state'
		.field public valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> '<>t__builder'
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
			01 00 00 00 00
		)
		.field public int32 'value'
		.field private valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter '<>u__1'

		// Methods
		.method private final hidebysig newslot virtual 
			instance void MoveNext () cil managed 
		{
			.override method instance void [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext()
			// Method begins at RVA 0x21d0
			// Header size: 12
			// Code size: 157 (0x9d)
			.maxstack 3
			.locals init (
				[0] int32,
				[1] int32,
				[2] valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter,
				[3] valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable,
				[4] class [System.Runtime]System.Exception
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'
			IL_0006: stloc.0
			.try
			{
				IL_0007: ldloc.0
				IL_0008: brfalse.s IL_0041

				IL_000a: call valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable [System.Runtime]System.Threading.Tasks.Task::Yield()
				IL_000f: stloc.3
				IL_0010: ldloca.s 3
				IL_0012: call instance valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable::GetAwaiter()
				IL_0017: stloc.2
				IL_0018: ldloca.s 2
				IL_001a: call instance bool [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::get_IsCompleted()
				IL_001f: brtrue.s IL_005d

				IL_0021: ldarg.0
				IL_0022: ldc.i4.0
				IL_0023: dup
				IL_0024: stloc.0
				IL_0025: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'
				IL_002a: ldarg.0
				IL_002b: ldloc.2
				IL_002c: stfld valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>u__1'
				IL_0031: ldarg.0
				IL_0032: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
				IL_0037: ldloca.s 2
				IL_0039: ldarg.0
				IL_003a: call instance void valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::AwaitUnsafeOnCompleted<valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter, valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>>(!!0&, !!1&)
				IL_003f: leave.s IL_009c

				IL_0041: ldarg.0
				IL_0042: ldfld valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>u__1'
				IL_0047: stloc.2
				IL_0048: ldarg.0
				IL_0049: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>u__1'
				IL_004e: initobj [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter
				IL_0054: ldarg.0
				IL_0055: ldc.i4.m1
				IL_0056: dup
				IL_0057: stloc.0
				IL_0058: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'

				IL_005d: ldloca.s 2
				IL_005f: call instance void [System.Runtime]System.Runtime.CompilerServices.YieldAwaitable/YieldAwaiter::GetResult()
				IL_0064: ldarg.0
				IL_0065: ldfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'value'
				IL_006a: ldc.i4.7
				IL_006b: add
				IL_006c: stloc.1
				IL_006d: leave.s IL_0088
			} // end .try
			catch [System.Runtime]System.Exception
			{
				IL_006f: stloc.s 4
				IL_0071: ldarg.0
				IL_0072: ldc.i4.s -2
				IL_0074: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'
				IL_0079: ldarg.0
				IL_007a: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
				IL_007f: ldloc.s 4
				IL_0081: call instance void valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetException(class [System.Runtime]System.Exception)
				IL_0086: leave.s IL_009c
			} // end handler

			IL_0088: ldarg.0
			IL_0089: ldc.i4.s -2
			IL_008b: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'
			IL_0090: ldarg.0
			IL_0091: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
			IL_0096: ldloc.1
			IL_0097: call instance void valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetResult(!0)

			IL_009c: ret
		} // end of method '<ComputeAsync>d__17'::MoveNext

		.method private final hidebysig newslot virtual 
			instance void SetStateMachine (
				class [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine stateMachine
			) cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine::SetStateMachine(class [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine)
			// Method begins at RVA 0x228c
			// Header size: 1
			// Code size: 13 (0xd)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
			IL_0006: ldarg.1
			IL_0007: call instance void valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::SetStateMachine(class [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine)
			IL_000c: ret
		} // end of method '<ComputeAsync>d__17'::SetStateMachine

	} // end of class <ComputeAsync>d__17

	.class nested private auto ansi sealed beforefieldinit '<CountTo>d__18'<class .ctor T>
		extends [System.Runtime]System.Object
		implements class [System.Runtime]System.Collections.Generic.IEnumerable`1<int32>,
		           [System.Runtime]System.Collections.IEnumerable,
		           class [System.Runtime]System.Collections.Generic.IEnumerator`1<int32>,
		           [System.Runtime]System.Collections.IEnumerator,
		           [System.Runtime]System.IDisposable
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		.interfaceimpl type class [System.Runtime]System.Collections.Generic.IEnumerable`1<int32>
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		.interfaceimpl type [System.Runtime]System.Collections.IEnumerable
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		.interfaceimpl type class [System.Runtime]System.Collections.Generic.IEnumerator`1<int32>
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		.interfaceimpl type [System.Runtime]System.Collections.IEnumerator
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		.interfaceimpl type [System.Runtime]System.IDisposable
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 00 00 00
			)

		// Fields
		.field private int32 '<>1__state'
		.field private int32 '<>2__current'
		.field private int32 '<>l__initialThreadId'
		.field private int32 maximum
		.field public int32 '<>3__maximum'
		.field private int32 '<value>5__2'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x229a
			// Header size: 1
			// Code size: 25 (0x19)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [System.Runtime]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_000d: ldarg.0
			IL_000e: call int32 [System.Runtime]System.Environment::get_CurrentManagedThreadId()
			IL_0013: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>l__initialThreadId'
			IL_0018: ret
		} // end of method '<CountTo>d__18'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [System.Runtime]System.IDisposable::Dispose()
			// Method begins at RVA 0x22b4
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<CountTo>d__18'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [System.Runtime]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x22b8
			// Header size: 12
			// Code size: 92 (0x5c)
			.maxstack 3
			.locals init (
				[0] int32,
				[1] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldloc.0
			IL_0008: brfalse.s IL_0010

			IL_000a: ldloc.0
			IL_000b: ldc.i4.1
			IL_000c: beq.s IL_0035

			IL_000e: ldc.i4.0
			IL_000f: ret

			IL_0010: ldarg.0
			IL_0011: ldc.i4.m1
			IL_0012: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_0017: ldarg.0
			IL_0018: ldc.i4.0
			IL_0019: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<value>5__2'
			IL_001e: br.s IL_004c

			IL_0020: ldarg.0
			IL_0021: ldarg.0
			IL_0022: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<value>5__2'
			IL_0027: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>2__current'
			IL_002c: ldarg.0
			IL_002d: ldc.i4.1
			IL_002e: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_0033: ldc.i4.1
			IL_0034: ret

			IL_0035: ldarg.0
			IL_0036: ldc.i4.m1
			IL_0037: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_003c: ldarg.0
			IL_003d: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<value>5__2'
			IL_0042: stloc.1
			IL_0043: ldarg.0
			IL_0044: ldloc.1
			IL_0045: ldc.i4.1
			IL_0046: add
			IL_0047: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<value>5__2'

			IL_004c: ldarg.0
			IL_004d: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<value>5__2'
			IL_0052: ldarg.0
			IL_0053: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::maximum
			IL_0058: blt.s IL_0020

			IL_005a: ldc.i4.0
			IL_005b: ret
		} // end of method '<CountTo>d__18'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance int32 'System.Collections.Generic.IEnumerator<System.Int32>.get_Current' () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [System.Runtime]System.Collections.Generic.IEnumerator`1<int32>::get_Current()
			// Method begins at RVA 0x2320
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>2__current'
			IL_0006: ret
		} // end of method '<CountTo>d__18'::'System.Collections.Generic.IEnumerator<System.Int32>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [System.Runtime]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x2328
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [System.Runtime]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<CountTo>d__18'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [System.Runtime]System.Collections.IEnumerator::get_Current()
			.param [0]
				.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
					01 00 00 00 00
				)
			// Method begins at RVA 0x232f
			// Header size: 1
			// Code size: 12 (0xc)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>2__current'
			IL_0006: box [System.Runtime]System.Int32
			IL_000b: ret
		} // end of method '<CountTo>d__18'::System.Collections.IEnumerator.get_Current

		.method private final hidebysig newslot virtual 
			instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<int32> 'System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator' () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<!0> class [System.Runtime]System.Collections.Generic.IEnumerable`1<int32>::GetEnumerator()
			// Method begins at RVA 0x233c
			// Header size: 12
			// Code size: 55 (0x37)
			.maxstack 2
			.locals init (
				[0] class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_0006: ldc.i4.s -2
			IL_0008: bne.un.s IL_0022

			IL_000a: ldarg.0
			IL_000b: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>l__initialThreadId'
			IL_0010: call int32 [System.Runtime]System.Environment::get_CurrentManagedThreadId()
			IL_0015: bne.un.s IL_0022

			IL_0017: ldarg.0
			IL_0018: ldc.i4.0
			IL_0019: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>1__state'
			IL_001e: ldarg.0
			IL_001f: stloc.0
			IL_0020: br.s IL_0029

			IL_0022: ldc.i4.0
			IL_0023: newobj instance void class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::.ctor(int32)
			IL_0028: stloc.0

			IL_0029: ldloc.0
			IL_002a: ldarg.0
			IL_002b: ldfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>3__maximum'
			IL_0030: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::maximum
			IL_0035: ldloc.0
			IL_0036: ret
		} // end of method '<CountTo>d__18'::'System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator'

		.method private final hidebysig newslot virtual 
			instance class [System.Runtime]System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () cil managed 
		{
			.custom instance void [System.Runtime]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance class [System.Runtime]System.Collections.IEnumerator [System.Runtime]System.Collections.IEnumerable::GetEnumerator()
			// Method begins at RVA 0x237f
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<int32> class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator'()
			IL_0006: ret
		} // end of method '<CountTo>d__18'::System.Collections.IEnumerable.GetEnumerator

		// Properties
		.property instance int32 'System.Collections.Generic.IEnumerator<System.Int32>.Current'()
		{
			.get instance int32 ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'::'System.Collections.Generic.IEnumerator<System.Int32>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <CountTo>d__18


	// Fields
	.field public static literal int32 DefaultValue = int32(7)
	.field public static initonly string StaticValue
	.field private !T _value
	.field private string '<Name>k__BackingField'
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)
	.field private class [System.Runtime]System.EventHandler Changed
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
		01 00 02 00 00
	)
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)

	// Methods
	.method private hidebysig specialname rtspecialname static 
		void .cctor () cil managed 
	{
		// Method begins at RVA 0x2050
		// Header size: 1
		// Code size: 11 (0xb)
		.maxstack 8

		IL_0000: ldstr "initialized"
		IL_0005: stsfld string class ILCorpus.Sample.RepresentativeType`1<!T>::StaticValue
		IL_000a: ret
	} // end of method RepresentativeType`1::.cctor

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x205c
		// Header size: 1
		// Code size: 29 (0x1d)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [System.Runtime]System.Object::.ctor()
		IL_0006: ldarg.0
		IL_0007: call !!0 [System.Runtime]System.Activator::CreateInstance<!T>()
		IL_000c: stfld !0 class ILCorpus.Sample.RepresentativeType`1<!T>::_value
		IL_0011: ldarg.0
		IL_0012: ldsfld string [System.Runtime]System.String::Empty
		IL_0017: call instance void class ILCorpus.Sample.RepresentativeType`1<!T>::set_Name(string)
		IL_001c: ret
	} // end of method RepresentativeType`1::.ctor

	.method public hidebysig specialname 
		instance string get_Name () cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x207a
		// Header size: 1
		// Code size: 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld string class ILCorpus.Sample.RepresentativeType`1<!T>::'<Name>k__BackingField'
		IL_0006: ret
	} // end of method RepresentativeType`1::get_Name

	.method public hidebysig specialname 
		instance void set_Name (
			string 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x2082
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld string class ILCorpus.Sample.RepresentativeType`1<!T>::'<Name>k__BackingField'
		IL_0007: ret
	} // end of method RepresentativeType`1::set_Name

	.method public hidebysig specialname 
		instance !T get_Value () cil managed 
	{
		// Method begins at RVA 0x208b
		// Header size: 1
		// Code size: 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld !0 class ILCorpus.Sample.RepresentativeType`1<!T>::_value
		IL_0006: ret
	} // end of method RepresentativeType`1::get_Value

	.method public hidebysig specialname 
		instance void set_Value (
			!T 'value'
		) cil managed 
	{
		// Method begins at RVA 0x2093
		// Header size: 1
		// Code size: 30 (0x1e)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld !0 class ILCorpus.Sample.RepresentativeType`1<!T>::_value
		IL_0007: ldarg.0
		IL_0008: ldfld class [System.Runtime]System.EventHandler class ILCorpus.Sample.RepresentativeType`1<!T>::Changed
		IL_000d: dup
		IL_000e: brtrue.s IL_0012

		IL_0010: pop
		IL_0011: ret

		IL_0012: ldarg.0
		IL_0013: ldsfld class [System.Runtime]System.EventArgs [System.Runtime]System.EventArgs::Empty
		IL_0018: callvirt instance void [System.Runtime]System.EventHandler::Invoke(object, class [System.Runtime]System.EventArgs)
		IL_001d: ret
	} // end of method RepresentativeType`1::set_Value

	.method public hidebysig specialname 
		instance void add_Changed (
			class [System.Runtime]System.EventHandler 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = (
			01 00 02 00 00
		)
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x20b4
		// Header size: 12
		// Code size: 41 (0x29)
		.maxstack 3
		.locals init (
			[0] class [System.Runtime]System.EventHandler,
			[1] class [System.Runtime]System.EventHandler,
			[2] class [System.Runtime]System.EventHandler
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class [System.Runtime]System.EventHandler class ILCorpus.Sample.RepresentativeType`1<!T>::Changed
		IL_0006: stloc.0
		// loop start (head: IL_0007)
			IL_0007: ldloc.0
			IL_0008: stloc.1
			IL_0009: ldloc.1
			IL_000a: ldarg.1
			IL_000b: call class [System.Runtime]System.Delegate [System.Runtime]System.Delegate::Combine(class [System.Runtime]System.Delegate, class [System.Runtime]System.Delegate)
			IL_0010: castclass [System.Runtime]System.EventHandler
			IL_0015: stloc.2
			IL_0016: ldarg.0
			IL_0017: ldflda class [System.Runtime]System.EventHandler class ILCorpus.Sample.RepresentativeType`1<!T>::Changed
			IL_001c: ldloc.2
			IL_001d: ldloc.1
			IL_001e: call !!0 [System.Threading]System.Threading.Interlocked::CompareExchange<class [System.Runtime]System.EventHandler>(!!0&, !!0, !!0)
			IL_0023: stloc.0
			IL_0024: ldloc.0
			IL_0025: ldloc.1
			IL_0026: bne.un.s IL_0007
		// end loop
		IL_0028: ret
	} // end of method RepresentativeType`1::add_Changed

	.method public hidebysig specialname 
		instance void remove_Changed (
			class [System.Runtime]System.EventHandler 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = (
			01 00 02 00 00
		)
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x20ec
		// Header size: 12
		// Code size: 41 (0x29)
		.maxstack 3
		.locals init (
			[0] class [System.Runtime]System.EventHandler,
			[1] class [System.Runtime]System.EventHandler,
			[2] class [System.Runtime]System.EventHandler
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class [System.Runtime]System.EventHandler class ILCorpus.Sample.RepresentativeType`1<!T>::Changed
		IL_0006: stloc.0
		// loop start (head: IL_0007)
			IL_0007: ldloc.0
			IL_0008: stloc.1
			IL_0009: ldloc.1
			IL_000a: ldarg.1
			IL_000b: call class [System.Runtime]System.Delegate [System.Runtime]System.Delegate::Remove(class [System.Runtime]System.Delegate, class [System.Runtime]System.Delegate)
			IL_0010: castclass [System.Runtime]System.EventHandler
			IL_0015: stloc.2
			IL_0016: ldarg.0
			IL_0017: ldflda class [System.Runtime]System.EventHandler class ILCorpus.Sample.RepresentativeType`1<!T>::Changed
			IL_001c: ldloc.2
			IL_001d: ldloc.1
			IL_001e: call !!0 [System.Threading]System.Threading.Interlocked::CompareExchange<class [System.Runtime]System.EventHandler>(!!0&, !!0, !!0)
			IL_0023: stloc.0
			IL_0024: ldloc.0
			IL_0025: ldloc.1
			IL_0026: bne.un.s IL_0007
		// end loop
		IL_0028: ret
	} // end of method RepresentativeType`1::remove_Changed

	.method public hidebysig 
		instance int32 Add (
			int32 left,
			int32 right
		) cil managed 
	{
		// Method begins at RVA 0x2121
		// Header size: 1
		// Code size: 4 (0x4)
		.maxstack 8

		IL_0000: ldarg.1
		IL_0001: ldarg.2
		IL_0002: add
		IL_0003: ret
	} // end of method RepresentativeType`1::Add

	.method public hidebysig 
		instance !!TResult Convert<TResult> (
			class [System.Runtime]System.Func`2<!T, !!TResult> converter
		) cil managed 
	{
		.param type TResult
			.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
				01 00 02 00 00
			)
		// Method begins at RVA 0x2126
		// Header size: 1
		// Code size: 13 (0xd)
		.maxstack 8

		IL_0000: ldarg.1
		IL_0001: ldarg.0
		IL_0002: ldfld !0 class ILCorpus.Sample.RepresentativeType`1<!T>::_value
		IL_0007: callvirt instance !1 class [System.Runtime]System.Func`2<!T, !!TResult>::Invoke(!0)
		IL_000c: ret
	} // end of method RepresentativeType`1::Convert

	.method public hidebysig 
		instance class [System.Runtime]System.Threading.Tasks.Task`1<int32> ComputeAsync (
			int32 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.AsyncStateMachineAttribute::.ctor(class [System.Runtime]System.Type) = (
			01 00 38 49 4c 43 6f 72 70 75 73 2e 53 61 6d 70
			6c 65 2e 52 65 70 72 65 73 65 6e 74 61 74 69 76
			65 54 79 70 65 60 31 2b 3c 43 6f 6d 70 75 74 65
			41 73 79 6e 63 3e 64 5f 5f 31 37 00 00
		)
		// Method begins at RVA 0x2134
		// Header size: 12
		// Code size: 55 (0x37)
		.maxstack 2
		.locals init (
			[0] valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>
		)

		IL_0000: ldloca.s 0
		IL_0002: call valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<!0> valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::Create()
		IL_0007: stfld valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
		IL_000c: ldloca.s 0
		IL_000e: ldarg.1
		IL_000f: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'value'
		IL_0014: ldloca.s 0
		IL_0016: ldc.i4.m1
		IL_0017: stfld int32 valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>1__state'
		IL_001c: ldloca.s 0
		IL_001e: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
		IL_0023: ldloca.s 0
		IL_0025: call instance void valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::Start<valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>>(!!0&)
		IL_002a: ldloca.s 0
		IL_002c: ldflda valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32> valuetype ILCorpus.Sample.RepresentativeType`1/'<ComputeAsync>d__17'<!T>::'<>t__builder'
		IL_0031: call instance class [System.Runtime]System.Threading.Tasks.Task`1<!0> valuetype [System.Runtime]System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<int32>::get_Task()
		IL_0036: ret
	} // end of method RepresentativeType`1::ComputeAsync

	.method public hidebysig 
		instance class [System.Runtime]System.Collections.Generic.IEnumerable`1<int32> CountTo (
			int32 maximum
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [System.Runtime]System.Type) = (
			01 00 33 49 4c 43 6f 72 70 75 73 2e 53 61 6d 70
			6c 65 2e 52 65 70 72 65 73 65 6e 74 61 74 69 76
			65 54 79 70 65 60 31 2b 3c 43 6f 75 6e 74 54 6f
			3e 64 5f 5f 31 38 00 00
		)
		// Method begins at RVA 0x2177
		// Header size: 1
		// Code size: 15 (0xf)
		.maxstack 8

		IL_0000: ldc.i4.s -2
		IL_0002: newobj instance void class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::.ctor(int32)
		IL_0007: dup
		IL_0008: ldarg.1
		IL_0009: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<CountTo>d__18'<!T>::'<>3__maximum'
		IL_000e: ret
	} // end of method RepresentativeType`1::CountTo

	.method public hidebysig 
		instance class [System.Runtime]System.Func`2<int32, int32> CreateAdder (
			int32 offset
		) cil managed 
	{
		// Method begins at RVA 0x2187
		// Header size: 1
		// Code size: 24 (0x18)
		.maxstack 8

		IL_0000: newobj instance void class ILCorpus.Sample.RepresentativeType`1/'<>c__DisplayClass19_0'<!T>::.ctor()
		IL_0005: dup
		IL_0006: ldarg.1
		IL_0007: stfld int32 class ILCorpus.Sample.RepresentativeType`1/'<>c__DisplayClass19_0'<!T>::offset
		IL_000c: ldftn instance int32 class ILCorpus.Sample.RepresentativeType`1/'<>c__DisplayClass19_0'<!T>::'<CreateAdder>b__0'(int32)
		IL_0012: newobj instance void class [System.Runtime]System.Func`2<int32, int32>::.ctor(object, native int)
		IL_0017: ret
	} // end of method RepresentativeType`1::CreateAdder

	// Events
	.event [System.Runtime]System.EventHandler Changed
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = (
			01 00 02 00 00
		)
		.addon instance void ILCorpus.Sample.RepresentativeType`1::add_Changed(class [System.Runtime]System.EventHandler)
		.removeon instance void ILCorpus.Sample.RepresentativeType`1::remove_Changed(class [System.Runtime]System.EventHandler)
	}


	// Properties
	.property instance string Name()
	{
		.get instance string ILCorpus.Sample.RepresentativeType`1::get_Name()
		.set instance void ILCorpus.Sample.RepresentativeType`1::set_Name(string)
	}
	.property instance !T Value()
	{
		.get instance !0 ILCorpus.Sample.RepresentativeType`1::get_Value()
		.set instance void ILCorpus.Sample.RepresentativeType`1::set_Value(!0)
	}

} // end of class ILCorpus.Sample.RepresentativeType`1

.class interface public auto ansi abstract import beforefieldinit ILCorpus.Sample.ICorpusComContract
{
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = (
		01 00 01 00 00
	)
	.custom instance void [System.Runtime]System.Runtime.InteropServices.ComVisibleAttribute::.ctor(bool) = (
		01 00 01 00 00
	)
	.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.GuidAttribute::.ctor(string) = (
		01 00 24 35 38 38 38 33 30 34 35 2d 39 32 62 34
		2d 34 63 39 66 2d 61 34 32 61 2d 33 65 32 35 61
		39 62 39 30 66 37 38 00 00
	)
	.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.InterfaceTypeAttribute::.ctor(valuetype [System.Runtime.InteropServices]System.Runtime.InteropServices.ComInterfaceType) = (
		01 00 00 00 00 00 00 00
	)
	// Methods
	.method public hidebysig newslot abstract virtual 
		instance string Echo (
			string marshal(bstr) 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.DispIdAttribute::.ctor(int32) = (
			01 00 01 00 00 00 00 00
		)
	} // end of method ICorpusComContract::Echo

	.method public hidebysig specialname newslot abstract virtual 
		instance int32 get_Number () cil managed 
	{
	} // end of method ICorpusComContract::get_Number

	.method public hidebysig specialname newslot abstract virtual 
		instance void set_Number (
			int32 'value'
		) cil managed 
	{
	} // end of method ICorpusComContract::set_Number

	// Properties
	.property instance int32 Number()
	{
		.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.DispIdAttribute::.ctor(int32) = (
			01 00 02 00 00 00 00 00
		)
		.get instance int32 ILCorpus.Sample.ICorpusComContract::get_Number()
		.set instance void ILCorpus.Sample.ICorpusComContract::set_Number(int32)
	}

} // end of class ILCorpus.Sample.ICorpusComContract

.class public auto ansi sealed beforefieldinit ILCorpus.Sample.CorpusComClass
	extends [System.Runtime]System.Object
	implements ILCorpus.Sample.ICorpusComContract
{
	.custom instance void [System.Runtime]System.Runtime.InteropServices.ComVisibleAttribute::.ctor(bool) = (
		01 00 01 00 00
	)
	.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.GuidAttribute::.ctor(string) = (
		01 00 24 34 33 32 33 39 38 65 31 2d 33 33 34 66
		2d 34 35 61 66 2d 38 33 38 37 2d 38 32 66 33 34
		62 38 61 63 35 32 36 00 00
	)
	.custom instance void [System.Runtime.InteropServices]System.Runtime.InteropServices.ClassInterfaceAttribute::.ctor(valuetype [System.Runtime.InteropServices]System.Runtime.InteropServices.ClassInterfaceType) = (
		01 00 00 00 00 00 00 00
	)
	// Fields
	.field private int32 '<Number>k__BackingField'
	.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)

	// Methods
	.method public final hidebysig specialname newslot virtual 
		instance int32 get_Number () cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x21a0
		// Header size: 1
		// Code size: 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld int32 ILCorpus.Sample.CorpusComClass::'<Number>k__BackingField'
		IL_0006: ret
	} // end of method CorpusComClass::get_Number

	.method public final hidebysig specialname newslot virtual 
		instance void set_Number (
			int32 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x21a8
		// Header size: 1
		// Code size: 8 (0x8)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld int32 ILCorpus.Sample.CorpusComClass::'<Number>k__BackingField'
		IL_0007: ret
	} // end of method CorpusComClass::set_Number

	.method public final hidebysig newslot virtual 
		instance string Echo (
			string 'value'
		) cil managed 
	{
		.custom instance void [System.Runtime]System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = (
			01 00 01 00 00
		)
		// Method begins at RVA 0x21b1
		// Header size: 1
		// Code size: 2 (0x2)
		.maxstack 8

		IL_0000: ldarg.1
		IL_0001: ret
	} // end of method CorpusComClass::Echo

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x21b4
		// Header size: 1
		// Code size: 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [System.Runtime]System.Object::.ctor()
		IL_0006: ret
	} // end of method CorpusComClass::.ctor

	// Properties
	.property instance int32 Number()
	{
		.get instance int32 ILCorpus.Sample.CorpusComClass::get_Number()
		.set instance void ILCorpus.Sample.CorpusComClass::set_Number(int32)
	}

} // end of class ILCorpus.Sample.CorpusComClass

