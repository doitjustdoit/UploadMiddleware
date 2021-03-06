﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UploadMiddleware.Core;
using UploadMiddleware.Core.Processors;

namespace UploadMiddleware.LocalStorage
{
    public class LocalStorageCheckChunkProcessor : ICheckChunkProcessor
    {
        private static readonly MD5 Md5 = MD5.Create();
        private ChunkedUploadLocalStorageConfigure Configure { get; }
        public LocalStorageCheckChunkProcessor(ChunkedUploadLocalStorageConfigure configure)
        {
            Configure = configure;
        }
        //public Dictionary<string, string> FormData { get; } = new Dictionary<string, string>();
        public async Task<ResponseResult> Process(IQueryCollection query, IFormCollection form, IHeaderDictionary headers, HttpRequest request)
        {
            if (!headers.TryGetValue(ConstConfigs.FileMd5HeaderKey, out var md5) || string.IsNullOrWhiteSpace(md5))
            {
                return await Task.FromResult(new ResponseResult
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    ErrorMsg = "The md5 value of the file cannot be empty."
                });
            }
            if (md5.ToString().Length != 32)
            {
                return await Task.FromResult(new ResponseResult
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    ErrorMsg = "不合法的MD5值."
                });
            }

            if (!headers.TryGetValue(ConstConfigs.ChunkMd5HeaderKey, out var chunkMd5) || string.IsNullOrWhiteSpace(chunkMd5))
            {
                return await Task.FromResult(new ResponseResult
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    ErrorMsg = "The md5 value of the chunk cannot be empty."
                });
            }
            if (chunkMd5.ToString().Length != 32)
            {
                return await Task.FromResult(new ResponseResult
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    ErrorMsg = "不合法的MD5值"
                });
            }

            if (!headers.TryGetValue(ConstConfigs.ChunkHeaderKey, out var chunkValue) || string.IsNullOrWhiteSpace(chunkValue) || !int.TryParse(chunkValue, out var chunk))
            {
                return await Task.FromResult(new ResponseResult
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    ErrorMsg = "分片索引不能为空"
                });
            }



            var dir = Path.Combine(Configure.RootDirectory, "chunks", md5);
            if (!Directory.Exists(dir))
            {
                return await Task.FromResult(new ResponseResult
                {
                    Content = new { exist = false }
                });
            }

            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.GetFiles();
            if (files.Length == 0)
            {
                return await Task.FromResult(new ResponseResult
                {
                    Content = new { exist = false }
                });
            }
            var extensionName = Path.GetExtension(files.First().Name.Replace(".$chunk", ""));
            var url = Path.Combine(dir, $"{chunk}{extensionName}.$chunk");
            if (!File.Exists(url))
            {
                return await Task.FromResult(new ResponseResult
                {
                    Content = new { exist = false }
                });
            }
            return await Task.FromResult(new ResponseResult
            {
                Content = new { exist = (await GetFileMd5(url)).Equals(chunkMd5, StringComparison.OrdinalIgnoreCase) }
            });
        }

        private static async Task<string> GetFileMd5(string filepath)
        {
            await using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bufferSize = 1048576;
            var buff = new byte[bufferSize];
            Md5.Initialize();
            long offset = 0;
            while (offset < fs.Length)
            {
                long readSize = bufferSize;
                if (offset + readSize > fs.Length)
                    readSize = fs.Length - offset;
                fs.Read(buff, 0, Convert.ToInt32(readSize));
                if (offset + readSize < fs.Length)
                    Md5.TransformBlock(buff, 0, Convert.ToInt32(readSize), buff, 0);
                else
                    Md5.TransformFinalBlock(buff, 0, Convert.ToInt32(readSize));
                offset += bufferSize;
            }
            if (offset >= fs.Length)
            {
                var result = Md5.Hash;
                //Md5.Clear();
                var sb = new StringBuilder(32);
                foreach (var t in result)
                    sb.Append(t.ToString("X2"));

                return sb.ToString();
            }

            return null;
        }
    }
}
