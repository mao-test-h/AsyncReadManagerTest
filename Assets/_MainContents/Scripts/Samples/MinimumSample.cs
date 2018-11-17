namespace MainContents
{
    using System.Runtime.InteropServices;
    using System;
    using UnityEngine;

    using Unity.Collections;
    using Unity.IO.LowLevel.Unsafe;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// AsyncReadManager.Readの実行テスト(最小構成)
    /// </summary>
    public sealed unsafe class MinimumSample : MonoBehaviour
    {
        /// <summary>
        /// 非同期読み用 ハンドル
        /// </summary>
        /// <remarks>※Disposeを呼ばないとメモリリークする</remarks>
        ReadHandle _readHandle;

        /// <summary>
        /// 非同期読みデータのバッファなど
        /// </summary>
        NativeArray<ReadCommand> _readCommand;


        /// <summary>
        /// 読み込み開始
        /// </summary>
        public void ReadData()
        {
            // コマンド発行用にファイルサイズを取得
            var fileInfo = new System.IO.FileInfo(Constants.SampleDataPath);
            long fileSize = fileInfo.Length;

            // コマンド生成
            this._readCommand = new NativeArray<ReadCommand>(1, Allocator.Persistent);
            this._readCommand[0] = new ReadCommand
            {
                Offset = 0,
                Size = fileSize,
                Buffer = (byte*)UnsafeUtility.Malloc(fileSize, 16, Allocator.Persistent),
            };

            // 読み込み開始
            this._readHandle = AsyncReadManager.Read(Constants.SampleDataPath, (ReadCommand*)this._readCommand.GetUnsafePtr(), 1);
        }

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

                // 以下のコードで(void*)を直接文字列に変換することが可能。
                // → 但しデータが全てマネージドヒープに乗ってしまう。
                string ret = Marshal.PtrToStringAnsi((IntPtr)this._readCommand[0].Buffer);
                Debug.Log($"    <color=red>--- Data : {ret}</color>");

                this.ReleaseReadData();
                Debug.Log("Complete !!!");
            }
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
    }
}
