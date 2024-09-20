namespace GeneralUpdate.Differential.Binary
{
    public interface IChecksum
    {
        long Value { get; }

        void Reset();

        void Update(int value);

        void Update(byte[] buffer);

        void Update(byte[] buffer, int offset, int count);
    }
}