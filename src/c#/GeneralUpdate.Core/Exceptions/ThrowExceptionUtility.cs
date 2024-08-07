﻿using GeneralUpdate.Core.Exceptions.CustomArgs;
using System;
using System.IO;

namespace GeneralUpdate.Core.Exceptions
{
    internal sealed class ThrowExceptionUtility
    {
        public static void ThrowGeneralUpdateException(ExceptionArgs args)
            => Throw<Exception>(args.ToString(), args);

        #region Common

        public static void ThrowFileNotFound(string file) => Throw<FileNotFoundException>($"File cannot be accessed {file}!");

        public static void ThrowIfNull(string errorMessage = null)
        {
            errorMessage = errorMessage ?? "Parameter cannot be null";
            Throw<ArgumentException>(errorMessage);
        }

        /// <summary>
        /// Checks if an object is empty and throws an exception if it is
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="paramName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void ThrowIfNull(object obj, string paramName)
        {
            if (obj == null)
                Throw<ArgumentNullException>(paramName);
        }

        /// <summary>
        /// Checks if the string is empty or blank, and throws an exception if it is.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="paramName"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void ThrowIfNullOrWhiteSpace(string str, string paramName)
        {
            if (string.IsNullOrWhiteSpace(str))
                Throw<ArgumentException>("Parameter cannot be null or whitespace", paramName);
        }

        /// <summary>
        /// Basic method of exception declaration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Throw<T>(string message, params object[] args) where T : Exception, new()
            => throw (T)Activator.CreateInstance(typeof(T), message, args);

        #endregion Common
    }
}