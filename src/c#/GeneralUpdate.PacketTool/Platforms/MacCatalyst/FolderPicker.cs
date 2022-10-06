using Foundation;
using GeneralUpdate.Infrastructure.DataServices.Pick;
using UIKit;

namespace GeneralUpdate.PacketTool.Platforms.MacCatalyst
{
    public class FolderPicker : IFolderPickerService
    {
        class PickerDelegate : UIDocumentPickerDelegate
        {
            public Action<NSUrl[]> PickHandler { get; set; }

            public override void WasCancelled(UIDocumentPickerViewController controller)
                => PickHandler?.Invoke(null);

            public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
                => PickHandler?.Invoke(urls);

            public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
                => PickHandler?.Invoke(new NSUrl[] { url });
        }

        static void GetFileResults(NSUrl[] urls, TaskCompletionSource<string> tcs)
        {
            try
            {
                tcs.TrySetResult(urls?[0]?.ToString() ?? "");
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        public async Task<string> PickFolderTaskAsync()
        {
            var allowedTypes = new string[]
            {
              "public.folder"
            };

            var picker = new UIDocumentPickerViewController(allowedTypes, UIDocumentPickerMode.Open);
            var tcs = new TaskCompletionSource<string>();

            picker.Delegate = new PickerDelegate
            {
                PickHandler = urls => GetFileResults(urls, tcs)
            };

            if (picker.PresentationController != null)
            {
                picker.PresentationController.Delegate =
                    new UIPresentationControllerDelegate(() => GetFileResults(null, tcs));
            }

            var parentController = Platform.GetCurrentUIViewController();

            parentController.PresentViewController(picker, true, null);

            return await tcs.Task;
        }

        internal class UIPresentationControllerDelegate : UIAdaptivePresentationControllerDelegate
        {
            Action dismissHandler;

            internal UIPresentationControllerDelegate(Action dismissHandler)
                => this.dismissHandler = dismissHandler;

            public override void DidDismiss(UIPresentationController presentationController)
            {
                dismissHandler?.Invoke();
                dismissHandler = null;
            }

            protected override void Dispose(bool disposing)
            {
                dismissHandler?.Invoke();
                base.Dispose(disposing);
            }
        }
    }
}
