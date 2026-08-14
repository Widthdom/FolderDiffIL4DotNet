using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ILCorpus.Sample
{
    public sealed class RepresentativeType<T>
        where T : class, new()
    {
        public const int DefaultValue = 7;
        public static readonly string StaticValue;

        private T _value;

        static RepresentativeType()
        {
            StaticValue = "initialized";
        }

        public RepresentativeType()
        {
            _value = new T();
            Name = string.Empty;
        }

        public string Name { get; set; }

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Changed;

        public int Add(int left, int right)
        {
            return left + right;
        }

        public TResult Convert<TResult>(Func<T, TResult> converter)
        {
            return converter(_value);
        }

        public async Task<int> ComputeAsync(int value)
        {
            await Task.Yield();
            return value + DefaultValue;
        }

        public IEnumerable<int> CountTo(int maximum)
        {
            for (var value = 0; value < maximum; value++)
            {
                yield return value;
            }
        }

        public Func<int, int> CreateAdder(int offset)
        {
            return value => value + offset;
        }
    }

    [ComVisible(true)]
    [ComImport]
    [Guid("58883045-92b4-4c9f-a42a-3e25a9b90f78")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICorpusComContract
    {
        [DispId(1)]
        string Echo([MarshalAs(UnmanagedType.BStr)] string value);

        [DispId(2)]
        int Number { get; set; }
    }

    [ComVisible(true)]
    [Guid("432398e1-334f-45af-8387-82f34b8ac526")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class CorpusComClass : ICorpusComContract
    {
        public int Number { get; set; }

        public string Echo(string value)
        {
            return value;
        }
    }
}
