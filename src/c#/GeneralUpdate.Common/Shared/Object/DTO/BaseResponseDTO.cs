namespace GeneralUpdate.Common.Shared.Object
{
    public class BaseResponseDTO<TBody>
    {
        public int Code { get; set; }

        public TBody Body { get; set; }

        public string Message { get; set; }
    }
}