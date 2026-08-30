using System;
using System.Collections.Generic;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 运行时数据文件清单。
    /// StreamingAssets 在 Android 上是 URI，运行时不能通过 Directory 扫描，
    /// 因此由 Editor 在导表时生成清单供运行时定位数据文件。
    /// </summary>
    [Serializable]
    public sealed class DataManifest
    {
        public int version = 1;
        public List<DataManifestEntry> entries = new();
    }

    [Serializable]
    public sealed class DataManifestEntry
    {
        /// <summary>默认使用数据类名（去掉 Container 后缀）作为 key。</summary>
        public string key;

        /// <summary>相对于 StreamingAssets 的路径，不包含扩展名。</summary>
        public string relativePath;

        /// <summary>实际文件扩展名，例如 .json 或 .fink。</summary>
        public string extension;

        public string format;
        public long size;
        public string hash;
    }
}
