﻿//------------------------------------------------------------------------------
//
//      CosmosEngine - The Lightweight Unity3D Game Develop Framework
// 
//                     Version 0.8 (20140904)
//                     Copyright © 2011-2014
//                   MrKelly <23110388@qq.com>
//              https://github.com/mr-kelly/CosmosEngine
//
//------------------------------------------------------------------------------
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Load www, A wrapper of WWW.  
/// Current version, loaded Resource will never release in memory
/// </summary>
[CDependencyClass(typeof(CResourceModule))]
public class CWWWLoader : CBaseResourceLoader
{
    // 前几项用于监控器
    private static IEnumerator CachedWWWLoaderMonitorCoroutine; // 专门监控WWW的协程
    const int MAX_WWW_COUNT = 5;
    private static int WWWLoadingCount = 0; // 有多少个WWW正在运作, 有上限的
    private static readonly Stack<CWWWLoader> WWWLoadersStack = new Stack<CWWWLoader>();  // WWWLoader的加载是后进先出! 有一个协程全局自我管理. 后来涌入的优先加载！
    
    public static event Action<string> WWWFinishCallback;
    
    public WWW Www;

    /// <summary>
    /// Use this to directly load WWW by Callback or Coroutine, pass a full URL.
    /// A wrapper of Unity's WWW class.
    /// </summary>
    public static CWWWLoader Load(string url, CLoaderDelgate callback = null)
    {
        var wwwLoader = AutoNew<CWWWLoader>(url, callback);
        return wwwLoader;
    }

    protected override void Init(string url)
    {
        base.Init(url);
        WWWLoadersStack.Push(this);  // 不执行开始加载，由www监控器协程控制

        if (CachedWWWLoaderMonitorCoroutine == null)
        {
            CachedWWWLoaderMonitorCoroutine = WWWLoaderMonitorCoroutine();
            CResourceModule.Instance.StartCoroutine(CachedWWWLoaderMonitorCoroutine);
        }
    }

    protected void StartLoad()
    {
        CResourceModule.Instance.StartCoroutine(CoLoad(Url));//开启协程加载Assetbundle，执行Callback
    }
    /// <summary>
    /// 协和加载Assetbundle，加载完后执行callback
    /// </summary>
    /// <param name="url">资源的url</param>
    /// <param name="callback"></param>
    /// <param name="callbackArgs"></param>
    /// <returns></returns>
    IEnumerator CoLoad(string url)
    {
        CResourceModule.LogRequest("WWW", url);

        System.DateTime beginTime = System.DateTime.Now;
        Www = new WWW(url);
        WWWLoadingCount++;

        //设置AssetBundle解压缩线程的优先级
        Www.threadPriority = Application.backgroundLoadingPriority;  // 取用全局的加载优先速度
        while (!Www.isDone)
        {
            Progress = Www.progress;
            yield return null;
        }

        yield return Www;
        WWWLoadingCount--;
		Progress = 1;
		
        if (!string.IsNullOrEmpty(Www.error))
        {
            string fileProtocol = CResourceModule.GetFileProtocol();
            if (url.StartsWith(fileProtocol))
            {
                string fileRealPath = url.Replace(fileProtocol, "");
                CDebug.LogError("File {0} Exist State: {1}", fileRealPath, System.IO.File.Exists(fileRealPath));

            }
            CDebug.LogError("[CWWWLoader:Error]" + Www.error + " " + url);

            OnFinish(null);
            yield break;
        }
        else
        {
            CResourceModule.LogLoadTime("WWW", url, beginTime);
            if (WWWFinishCallback != null)
                WWWFinishCallback(url);

            OnFinish(Www);
        }

#if UNITY_EDITOR  // 预防WWW加载器永不反初始化
        while (GetCount<CWWWLoader>() > 0)
            yield return null;

        yield return new WaitForSeconds(5f);

        while (Debug.isDebugBuild && !IsDisposed)
        {
            CDebug.LogError("[CWWWLoader]Not Disposed Yet! : {0}", this.Url);
            yield return null;
        }
#endif
    }

    protected override void DoDispose()
    {
        base.DoDispose();

        Www.Dispose();
        Www = null;
    }




    /// <summary>
    /// 监视器协程
    /// 超过最大WWWLoader时，挂起~
    /// 
    /// 后来的新loader会被优先加载
    /// </summary>
    /// <returns></returns>
    protected static IEnumerator WWWLoaderMonitorCoroutine()
    {
        //yield return new WaitForEndOfFrame(); // 第一次等待本帧结束
        yield return null;

        while (WWWLoadersStack.Count > 0)
        {
            if (CResourceModule.LoadByQueue)
            {
                while (GetCount<CWWWLoader>() != 0)
                    yield return null;
            }
            while (WWWLoadingCount >= MAX_WWW_COUNT)
            {
                yield return null;
            }

            var wwwLoader = WWWLoadersStack.Pop();
            wwwLoader.StartLoad();
        }

        CResourceModule.Instance.StopCoroutine(CachedWWWLoaderMonitorCoroutine);
        CachedWWWLoaderMonitorCoroutine = null;
    }

}
