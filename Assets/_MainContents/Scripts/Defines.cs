namespace MainContents
{
    using UnityEngine;

    public sealed class Constants
    {
        /// <summary>
        /// データ読み込みパス
        /// </summary>
        public readonly static string SampleDataPath = Application.dataPath + "/../Sample_full.csv";
    }

    public static class ParseExtensions
    {
        public static int ParseInt(this string str)
        {
            return int.Parse(str);
        }
    }
}
