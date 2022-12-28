using GeneralUpdate.Core.CustomAwaiter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Config.Handles
{
    /// <summary>
    /// JSON configuration file processing class .
    /// </summary>
    /// <typeparam name="TContent">json configuration file content.</typeparam>
    public class JsonHandle<TContent> : IHandle<TContent>, IAwaiter<JsonHandle<TContent>> where TContent : class
    {
        private bool _isCompleted;
        private Exception _exception;

        public bool IsCompleted { get => _isCompleted; private set => _isCompleted = value; }

        public void OnCompleted(Action continuation)
        {
            if (continuation != null) continuation.Invoke();
        }

        /// <summary>
        /// Read the content of the configuration file according to the path .
        /// </summary>
        /// <param name="path">file path.</param>
        /// <returns>file content.</returns>
        public Task<TContent> Read(string path)
        {
            try
            {
                var jsonText = File.ReadAllText(path);
                return Task.FromResult(JsonConvert.DeserializeObject<TContent>(jsonText));
            }
            catch (Exception ex)
            {
                throw new Exception($"read config error : {ex.Message} !", ex.InnerException);
            }
            finally
            {
                IsCompleted = true;
            }
        }

        /// <summary>
        /// Write the processed content to the configuration file .
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="content">file content.</param>
        /// <returns>is done.</returns>
        public async Task<bool> Write(TContent oldEntity, TContent newEntity)
        {
            try
            {
                var oldResult = GetPropertyValue<object>(oldEntity, "Content");
                var newResult = GetPropertyValue<object>(newEntity, "Content");
                var oldPath = GetPropertyValue<string>(oldEntity, "Path");
                string json = string.Empty;
                CopyValue(oldResult, newResult, ref json);
                File.WriteAllText(oldPath, json);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
            finally
            {
                IsCompleted = true;
            }
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Iterate over objects and copy values .
        /// </summary>
        /// <typeparam name="T">json object .</typeparam>
        /// <param name="source">original configuration file .</param>
        /// <param name="target">latest configuration file .</param>
        /// <param name="json">result json.</param>
        private void CopyValue<T>(T source, T target, ref string json) where T : class
        {
            try
            {
                JObject jSource = JObject.Parse(source.ToString());
                JObject jTarget = JObject.Parse(target.ToString());
                foreach (JProperty jProperty in jSource.Properties())
                {
                    var jFindObj = jTarget.Properties().FirstOrDefault(j => j.Name.Equals(jProperty.Name));
                    if (jFindObj != null)
                    {
                        jFindObj.Value = jProperty.Value;
                    }
                }
                json = JsonConvert.SerializeObject(jTarget);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        private TResult GetPropertyValue<TResult>(TContent entity, string propertyName)
        {
            TResult result = default(TResult);
            Type entityType = typeof(TContent);
            try
            {
                PropertyInfo info = entityType.GetProperty(propertyName);
                result = (TResult)info.GetValue(entity);
            }
            catch (ArgumentNullException ex)
            {
                throw _exception = new ArgumentNullException("'GetPropertyValue' The method executes abnormally !", ex);
            }
            catch (AmbiguousMatchException ex)
            {
                throw _exception = new AmbiguousMatchException("'GetPropertyValue' The method executes abnormally !", ex);
            }
            return result;
        }

        public JsonHandle<TContent> GetAwaiter() => this;

        public JsonHandle<TContent> GetResult()
        {
            if (_exception != null) ExceptionDispatchInfo.Capture(_exception).Throw();
            return this;
        }

        public async Task AsTask(JsonHandle<TContent> awaiter) => await awaiter;
    }
}