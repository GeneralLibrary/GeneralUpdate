namespace GeneralUpdate.Core.Domain.DTO
{
    public class BaseResponseDTO<TBody>
    {
        public int Code { get; set; }

        public TBody Body { get; set; }

        public string Message { get; set; }
    }
}