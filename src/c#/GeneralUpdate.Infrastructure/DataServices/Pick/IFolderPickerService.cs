namespace GeneralUpdate.Infrastructure.DataServices.Pick
{
    public interface IFolderPickerService
    {
        Task<string> PickFolderTaskAsync();
    }
}
