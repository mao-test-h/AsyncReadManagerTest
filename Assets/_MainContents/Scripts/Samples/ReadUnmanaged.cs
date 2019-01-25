//#define SHOW_DEBUG_PRINT

namespace MainContents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using UnityEngine;
    using UnityEngine.Assertions;
    using UnityEngine.Profiling;

    using Unity.Collections;
    using Unity.IO.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// CSV読み込みテスト(アンマネージド版)
    /// </summary>
    public unsafe sealed class ReadUnmanaged : MonoBehaviour
    {
        // -----------------------------------------
        #region // Constants

        /// <summary>
        /// ASCII CODE : Null
        /// </summary>
        const int NullCode = 0;

        /// <summary>
        /// ACSII CODE : カンマ
        /// </summary>
        const int CommaCode = 44;

        /// <summary>
        /// ACSII CODE : LF(改行)
        /// </summary>
        const int LineFeedCode = 10;

        #endregion // Constants

        // -----------------------------------------
        #region // Defines

        /// <summary>
        /// 文字列のポインタ
        /// </summary>
        /// <remarks>※Bilittableを考慮した結果、こう持ってみることにした。</remarks>
        public unsafe struct StringPtr : IDisposable
        {
            public byte* Data;
            public int Length;

            public void Dispose()
            {
                UnsafeUtility.Free(this.Data, Allocator.Persistent);
            }

            /// <summary>
            /// 文字列の取得
            /// </summary>
            public override string ToString()
            {
                byte[] ret = new byte[this.Length];
                Marshal.Copy((IntPtr)this.Data, ret, 0, this.Length);
                return System.Text.Encoding.UTF8.GetString(ret);
            }
        }

        /// <summary>
        /// CSVから取得できるデータ
        /// </summary>
        public unsafe struct CharacterData
        {
            // ※構造体をNativeArrayで持たせる都合上、stringや配列は使えないのでポインタで持たせている。
            public StringPtr Name;
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
        /// <remarks>アンマネージドヒープで保持</remarks>
        NativeArray<CharacterData> _charaData;

        /// <summary>
        /// 非同期読み用 ハンドル
        /// </summary>
        /// <remarks>※Disposeを呼ばないとメモリリークする</remarks>
        ReadHandle _readHandle;

        /// <summary>
        /// 非同期読みデータのバッファなど
        /// </summary>
        NativeArray<ReadCommand> _readCommand;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Update
        /// </summary>
        void Update()
        {
            if (this._readHandle.IsValid() && this._readHandle.Status != ReadStatus.InProgress)
            {
                // エラーハンドリング
                if (this._readHandle.Status != ReadStatus.Complete)
                {
                    Debug.LogError($"Read Error : {this._readHandle.Status}");
                    this.ReleaseReadData();
                    return;
                }

                Profiler.BeginSample(">>> Parse Unmanaged");

                // 読み込んだCSV(void*)をパース
                this.Parse();

                Profiler.EndSample();

                this.ReleaseReadData();
                Debug.Log("Complete ReadUnmanaged");
            }
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            this.Release();
        }

        #endregion // Unity Events

        // ----------------------------------------------------
        #region // Public Methods(Event)

        /// <summary>
        /// CSVの読み込み
        /// </summary>
        public void ReadData()
        {
            Profiler.BeginSample(">>> Command Unmanaged");

            // コマンド発行用にファイルサイズを取得
            var fileInfo = new System.IO.FileInfo(Constants.SampleDataPath);
            long fileSize = fileInfo.Length;

            // コマンド生成
            this._readCommand = new NativeArray<ReadCommand>(1, Allocator.Persistent);
            this._readCommand[0] = new ReadCommand
            {
                Offset = 0,
                Size = fileSize,
                Buffer = UnsafeUtility.Malloc(fileSize, UnsafeUtility.AlignOf<byte>(), Allocator.Persistent),
            };

            // 読み込み開始
            this._readHandle = AsyncReadManager.Read(Constants.SampleDataPath, (ReadCommand*)this._readCommand.GetUnsafePtr(), 1);

            Profiler.EndSample();
        }

        /// <summary>
        /// データ確認
        /// </summary>
        /// <param name="index">読み込んだデータのindex</param>
        public void ShowData(int index)
        {
            var data = this._charaData[index];
            Debug.Log($"<color=green>Name : {data.Name.ToString()}, HP : {data.HP}, MP : {data.MP}, Attack : {data.Attack}, Defense : {data.Defense}</color>");
        }

        /// <summary>
        /// 解放
        /// </summary>
        public void Release()
        {
            // ※Editor上でも解放忘れると残り続ける模様...
            if (this._charaData.IsCreated)
            {
                foreach (var data in this._charaData)
                {
                    data.Name.Dispose();
                }
                this._charaData.Dispose();
            }
        }

        #endregion // Public Methods(Event)

        // ----------------------------------------------------
        #region // Private Methods

        /// <summary>
        /// 読み込んだCSVデータのパース
        /// </summary>
        void Parse()
        {
            void* ptr = this._readCommand[0].Buffer;    // データのポインタ

            // ptrの内容をこちらに入れ直す。
            // (NativeArrayUnsafeUtility.ConvertExistingDataToNativeArrayで変換できない..? やり方が変かもしれないので要検証)
            var source = new NativeArray<byte>((int)this._readCommand[0].Size, Allocator.Temp);

            // 全データ数のカウント及びNativeArrayへの入れ直し
            {
                int iterator = 0;
                int maxRecordCount = 0; // 全データ数
                while (true)
                {
                    // 泥臭いが1byteずつチェックしていき「改行 == 1レコード」とみなしてカウントしていく。
                    byte val = Marshal.ReadByte((IntPtr)ptr, iterator);
                    if (val == NullCode) { break; }
                    else if (val == LineFeedCode) { ++maxRecordCount; }
                    source[iterator] = val;
                    ++iterator;
                }
                this._charaData = new NativeArray<CharacterData>(maxRecordCount, Allocator.Persistent);
            }

            // パース
            int rowIndex = 0;                       // カンマ間の内部のindex. → e.g...[0-1-2-3, 0-1, 0-1-2,..]
            int commaIndex = 0;                     // レコード中のカンマの位置
            int recordCount = 0;                    // レコード数
            var parseData = new CharacterData();    // 解析したデータを格納
            int sliceIndex = 0;                     // NativeSlice用の開始位置
            // TODO: Job化を検討できそう
            for (int i = 0; i < source.Length; ++i)
            {
                // こちらも同じく1byteずつチェック。
                byte val = source[i];

                // 終端が来たら終わり
                if (val == NullCode) { break; }

                if (val == CommaCode)
                {
                    // カンマを検知したらNativeSliceで切り取っていく
                    switch (commaIndex)
                    {
                        // 名前
                        case 0:
                            parseData.Name = this.SliceToStringPtr(source, sliceIndex, rowIndex, Allocator.Persistent);
                            break;
                        // HP
                        case 1:
                            parseData.HP = this.SliceToString(source, sliceIndex, rowIndex).ParseInt();
                            this.DebugLog("HP", parseData.HP);
                            break;
                        // MP
                        case 2:
                            parseData.MP = this.SliceToString(source, sliceIndex, rowIndex).ParseInt();
                            this.DebugLog("MP", parseData.MP);
                            break;
                        // Attack
                        case 3:
                            parseData.Attack = this.SliceToString(source, sliceIndex, rowIndex).ParseInt();
                            this.DebugLog("Attack", parseData.Attack);
                            break;
                    }
                    ++commaIndex;
                    rowIndex = 0;
                }
                else if (val == LineFeedCode)
                {
                    // ※この時点ではこの値じゃないとおかしいので念の為チェックしておく
                    Assert.IsTrue(commaIndex == 4);

                    // Defense
                    parseData.Defense = this.SliceToString(source, sliceIndex, rowIndex).ParseInt();
                    this.DebugLog("Defense", parseData.Defense);

                    // インスタンスの保持
                    this._charaData[recordCount] = parseData;
                    ++recordCount;

                    // 次のレコードを見に行く前にインスタンスを新規生成
                    parseData = new CharacterData();
                    commaIndex = 0;
                    rowIndex = 0;
                }
                else
                {
                    ++rowIndex;
                }

                if (val == CommaCode || val == LineFeedCode)
                {
                    // カンマ及び改行コードの次をindexをスライス開始位置とする。
                    sliceIndex = i + 1;
                }
            }
            source.Dispose();
        }

        /// <summary>
        /// 切り取ったバイト配列を新規で確保したメモリにコピーし、それを文字列用のポインタとして返す
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="start">切り取り開始位置</param>
        /// <param name="length">データの長さ</param>
        /// <param name="allocator">Allocator</param>
        /// <returns>文字列のポインタ</returns>
        StringPtr SliceToStringPtr(NativeArray<byte> source, int start, int length, Allocator allocator)
        {
            var slice = new NativeSlice<byte>(source, start, length);
            byte* arrPtr = (byte*)UnsafeUtility.Malloc(length, UnsafeUtility.AlignOf<byte>(), allocator);
            UnsafeUtility.MemClear(arrPtr, length);
            UnsafeUtility.MemCpy(arrPtr, NativeSliceUnsafeUtility.GetUnsafePtr(slice), length);
            return new StringPtr { Data = arrPtr, Length = length };
        }

        /// <summary>
        /// バイト配列を切り取って文字列に変換
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="start">切り取り開始位置</param>
        /// <param name="length">データの長さ</param>
        /// <returns>文字列</returns>
        string SliceToString(NativeArray<byte> source, int start, int length)
        {
            var slice = new NativeSlice<byte>(source, start, length);
            return System.Text.Encoding.UTF8.GetString(slice.ToArray());
        }

        /// <summary>
        /// 非同期読み用データ関連の破棄
        /// </summary>
        void ReleaseReadData()
        {
            this._readHandle.Dispose();
            UnsafeUtility.Free(this._readCommand[0].Buffer, Allocator.Persistent);
            this._readCommand.Dispose();
        }

        [System.Diagnostics.Conditional("SHOW_DEBUG_PRINT")]
        void DebugLog(string debugPrefix, int message)
        {
            Debug.Log($"    <color=red>{debugPrefix} : {message}</color>");
        }

        [System.Diagnostics.Conditional("SHOW_DEBUG_PRINT")]
        void DebugLog(string debugPrefix, string message)
        {
            Debug.Log($"    <color=red>{debugPrefix} : {message}</color>");
        }

        #endregion // Private Methods
    }
}
