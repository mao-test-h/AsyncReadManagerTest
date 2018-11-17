namespace MainContents
{
    using System.IO;
    using UnityEngine;
    using UnityEngine.Profiling;

    /// <summary>
    /// CSV読み込みテスト(マネージド版)
    /// </summary>
    public sealed class ReadManaged : MonoBehaviour
    {
        // ------------------------------
        #region // Defines

        /// <summary>
        /// CSVから取得できるデータ
        /// </summary>
        public struct CharacterData
        {
            public string Name;
            public int HP;
            public int MP;
            public int Attack;
            public int Defense;
        }

        #endregion // Defines

        // ------------------------------
        #region // Private Fields

        /// <summary>
        /// CSVから読み込んだデータのキャッシュ
        /// </summary>
        CharacterData[] _charaData = null;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Public Methods(Event)

        /// <summary>
        /// CSVの読み込み
        /// </summary>
        public void ReadData()
        {
            var lines = File.ReadAllLines(Constants.SampleDataPath);
            this._charaData = new CharacterData[lines.Length];

            Profiler.BeginSample(">>> Parse Managed");

            for (int i = 0; i < lines.Length; ++i)
            {
                var line = lines[i];
                var args = line.Split(new char[] { ',' });
                this._charaData[i] = new CharacterData
                {
                    Name = args[0],
                    HP = args[1].ParseInt(),
                    MP = args[2].ParseInt(),
                    Attack = args[3].ParseInt(),
                    Defense = args[4].ParseInt(),
                };
            }

            Profiler.EndSample();
            Debug.Log("Complete ReadManaged");
        }

        #endregion // Public Methods(Event)
    }
}
