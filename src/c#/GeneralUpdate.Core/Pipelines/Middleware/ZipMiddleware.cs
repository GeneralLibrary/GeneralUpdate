﻿using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Factory;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public class ZipMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.Updatefile, "In the unzipped file ..."));
            var version = context.Version;
            bool isUnzip = UnZip(context);
            if (!isUnzip) throw new Exception($"Unzip file failed , Version-{version.Version}  Hash-{version.Hash} !");
            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }

        /// <summary>
        /// UnZip
        /// </summary>
        /// <param name="zipfilepath"></param>
        /// <param name="unzippath"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        protected bool UnZip(BaseContext context)
        {
            try
            {
                bool isComplated = false;
                var generalZipfactory = new GeneralZipFactory();
                generalZipfactory.UnZipProgress += (sender, e) =>
                EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.Updatefile, "Updatting file..."));
                generalZipfactory.Completed += (sender, e) => isComplated = true;
                generalZipfactory.CreateOperate(MatchType(context.Format), context.Name, context.ZipfilePath, context.TargetPath, false, context.Encoding).
                    UnZip();
                return isComplated;
            }
            catch (Exception exception)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                return false;
            }
        }

        private OperationType MatchType(string extensionName)
        {
            OperationType type = OperationType.None;
            switch (extensionName)
            {
                case ".zip":
                    type = OperationType.GZip;
                    break;

                case ".7z":
                    type = OperationType.G7z;
                    break;
            }
            return type;
        }
    }
}