using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RemoteAccess
{
    [Serializable]
    public class AssemblyDependent : IDisposable
    {
        string _dllFile;
        /// <summary>
        /// cad程序域依赖_内存区(不可以卸载)
        /// </summary>
        private Assembly[] _cadAs;

        /// <summary>
        /// cad程序域依赖_映射区(不可以卸载)
        /// </summary>
        private Assembly[] _cadAsRef;

        /// <summary>
        /// 加载DLL成功后获取到的程序集
        /// </summary>
        public List<Assembly> MyLoadAssemblys { get; private set; }

        /// <summary>
        /// 当前域加载事件,运行时出错的话,就靠这个事件来解决
        /// </summary>
        public event ResolveEventHandler CurrentDomainAssemblyResolveEvent
        {
            add
            {
                AppDomain.CurrentDomain.AssemblyResolve += value;
            }
            remove
            {
                AppDomain.CurrentDomain.AssemblyResolve -= value;
            }
        }

        /// <summary>
        /// 链式加载dll依赖
        /// </summary>
        /// <param name="dllFile"></param>
        public AssemblyDependent(string dllFile)
        {
            _dllFile = Path.GetFullPath(dllFile);//相对路径要先转换 Path.GetFullPath(dllFile);

            //cad程序集的依赖
            _cadAs = AppDomain.CurrentDomain.GetAssemblies();

            //映射区
            _cadAsRef = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies();

            //被加载的都存放在这里
            MyLoadAssemblys = new List<Assembly>();
        }

        /// <summary>
        /// 返回的类型,描述加载的错误
        /// </summary>
        public class LoadDllMessage
        {
            public string Path;
            public bool LoadYes;

            public LoadDllMessage(string path, bool loadYes)
            {
                Path = path;
                LoadYes = loadYes;
            }

            public override string ToString()
            {
                if (LoadYes)
                {
                    return "加载成功:" + Path;
                }
                return "加载失败:" + Path;
            }
        }
        /// <summary>
        /// 加载信息集合
        /// </summary>
        List<LoadDllMessage> LoadYesList;


        bool _byteLoad;
        /// <summary>
        /// 加载程序集
        /// </summary>
        /// <param name="byteLoad">true字节加载,false文件加载</param>
        /// <returns>返回加载链的</returns>
        public void Load(bool byteLoad = true)
        {
            _byteLoad = byteLoad;
            if (!File.Exists(_dllFile))
            {
                throw new ArgumentNullException("路径不存在");
            }
            LoadYesList = new List<LoadDllMessage>();

            //查询加载链之后再逆向加载,确保前面不丢失
            var allRefs = GetAllRefPaths(_dllFile);
            allRefs.Reverse();

            foreach (var path in allRefs)
            {
                try
                {
                    //路径转程序集名
                    string assName = AssemblyName.GetAssemblyName(path).FullName;
                    //路径转程序集名
                    var assembly = _cadAs.FirstOrDefault(a => a.FullName == assName);
                    if (assembly != null)
                    {
                        LoadYesList.Add(new LoadDllMessage(path, false));//版本号没变不加载
                        continue;
                    }

                    byte[] buffer = null;
                    bool flag = true;
                    //实现字节加载
                    if (path == _dllFile)
                    {
                        LoadOK = true;
                    }
#if DEBUG
                    //为了实现Debug时候出现断点,见链接,加依赖
                    // https://www.cnblogs.com/DasonKwok/p/10510218.html
                    // https://www.cnblogs.com/DasonKwok/p/10523279.html

                    var dir = Path.GetDirectoryName(path);
                    var pdbName = Path.GetFileNameWithoutExtension(path) + ".pdb";
                    var pdbFullName = Path.Combine(dir, pdbName);
                    if (File.Exists(pdbFullName) && _byteLoad)
                    {
                        var pdbbuffer = File.ReadAllBytes(pdbFullName);
                        buffer = File.ReadAllBytes(path);
                        var ass = Assembly.Load(buffer, pdbbuffer);
                        MyLoadAssemblys.Add(ass);
                        flag = false;
                    }
#endif
                    if (flag)
                    {
                        Assembly ass = null;
                        if (_byteLoad)
                        {
                            buffer = File.ReadAllBytes(path);
                            ass = Assembly.Load(buffer);
                        }
                        else
                        {
                            ass = Assembly.LoadFile(path);
                        }
                        MyLoadAssemblys.Add(ass);
                    }
                    LoadYesList.Add(new LoadDllMessage(path, true));//加载成功
                }
                catch
                {
                    LoadYesList.Add(new LoadDllMessage(path, false));//错误造成
                }
            }
            MyLoadAssemblys.Reverse();
        }

        //链条后面的不再理会,因为相同的dll引用辨识无意义
        /// <summary>
        /// 第一个dll加载是否成功
        /// </summary>
        public bool LoadOK { get; private set; }

        /// <summary>
        /// 加载出错信息
        /// </summary>
        public string LoadErrorMessage
        {
            get
            {
                var sb = new StringBuilder();
                bool allyes = true;
                foreach (var item in LoadYesList)
                {
                    if (!item.LoadYes)
                    {
                        sb.Append(Environment.NewLine + "** 此文件已加载过,重复名称,重复版本号,本次不加载!");
                        sb.Append(Environment.NewLine + item.ToString());
                        sb.Append(Environment.NewLine);
                        allyes = false;
                    }
                }
                if (allyes)
                {
                    sb.Append(Environment.NewLine + "** 链式加载成功!");
                    sb.Append(Environment.NewLine);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 获取加载链
        /// </summary>
        /// <param name="dll"></param>
        /// <param name="dlls"></param>
        /// <returns></returns>
        List<string> GetAllRefPaths(string dll, List<string> dlls = null)
        {
            if (dlls == null)
            {
                dlls = new List<string>();
            }
            if (dlls.Contains(dll) || !File.Exists(dll))
            {
                return dlls;
            }
            dlls.Add(dll);

            //路径转程序集名
            string assName = AssemblyName.GetAssemblyName(dll).FullName;

            //在当前程序域的assemblyAs内存区和assemblyAsRef映射区找这个程序集名
            Assembly assemblyAs = _cadAs.FirstOrDefault(a => a.FullName == assName);
            Assembly assemblyAsRef;

            //内存区有表示加载过
            //映射区有表示查找过但没有加载(一般来说不存在.只是debug会注释掉Assembly.Load的时候用来测试)
            if (assemblyAs != null)
            {
                assemblyAsRef = assemblyAs;
            }
            else
            {
                assemblyAsRef = _cadAsRef.FirstOrDefault(a => a.FullName == assName);

                //内存区和映射区都没有的话就把dll加载到映射区,用来找依赖表
                if (assemblyAsRef == null)
                {
                    // assemblyAsRef = Assembly.ReflectionOnlyLoad(dll); 没有依赖会直接报错
                    var byteRef = File.ReadAllBytes(dll);
                    assemblyAsRef = Assembly.ReflectionOnlyLoad(byteRef);
                }
            }

            //遍历依赖,如果存在dll拖拉加载目录就加入dlls集合
            foreach (var assemblyName in assemblyAsRef.GetReferencedAssemblies())
            {
                //dll拖拉加载路径-搜索路径(可以增加到这个dll下面的所有文件夹?)
                string directoryName = Path.GetDirectoryName(dll);

                var path = directoryName + "\\" + assemblyName.Name;
                var paths = new string[]
                {
                    path + ".dll",
                    path + ".exe"
                };
                foreach (var patha in paths)
                {
                    GetAllRefPaths(patha, dlls);
                }
            }
            return dlls;
        }



        /// <summary>
        /// 递归删除文件夹目录及文件
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        static void DeleteFolder(string dir)
        {
            if (Directory.Exists(dir)) //如果存在这个文件夹删除之
            {
                foreach (string d in Directory.GetFileSystemEntries(dir))
                {
                    if (File.Exists(d))
                        File.Delete(d); //直接删除其中的文件
                    else
                        DeleteFolder(d); //递归删除子文件夹
                }
                Directory.Delete(dir, true); //删除已空文件夹
            }
        }

        /// <summary>
        /// Debug的时候删除obj目录,防止占用
        /// </summary>
        public void DebugDelObjFiles()
        {
            try
            {
                var filename = Path.GetFileNameWithoutExtension(_dllFile);
                var path = Path.GetDirectoryName(_dllFile);

                var pdb = path + "\\" + filename + ".pdb";
                if (File.Exists(pdb))
                {
                    File.Delete(pdb);
                }

                var list = path.Split('\\');
                if (list[list.Length - 1] == "Debug" && list[list.Length - 2] == "bin")
                {
                    var bin = path.LastIndexOf("bin");
                    var proj = path.Substring(0, bin);
                    var obj = proj + "obj";
                    DeleteFolder(obj);
                }
            }
            catch
            { }
        }



        #region Dispose
        public bool Disposed = false;

        /// <summary>
        /// 显式调用Dispose方法,继承IDisposable
        /// </summary>
        public void Dispose()
        {
            //由手动释放
            Dispose(true);
            //通知垃圾回收机制不再调用终结器(析构器)_跑了这里就不会跑析构函数了
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数,以备忘记了显式调用Dispose方法
        /// </summary>
        ~AssemblyDependent()
        {
            //由系统释放
            Dispose(false);
        }


        /// <summary>
        /// 释放
        /// </summary>
        /// <param name="ing"></param>
        protected virtual void Dispose(bool ing)
        {
            if (Disposed)
            {
                //不重复释放
                return;
            }
            //让类型知道自己已经被释放
            Disposed = true;

            GC.Collect();
        }
        #endregion
    }
}