using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

namespace RemoteAccess
{
    [Serializable]
    public class AssemblyDependent : IDisposable
    {
        public const string FileExtensionSln = "*.sln";
        public const string FileExtensionCsProj = "*.csproj";

        /// <summary>
        ///     加载信息集合
        /// </summary>
        private bool _byteLoad;

        /// <summary>
        ///     cad程序域依赖_内存区(不可以卸载)
        /// </summary>
        private Dictionary<string, Assembly> _cadAs;

        /// <summary>
        ///     cad程序域依赖_映射区(不可以卸载)
        /// </summary>
        private Dictionary<string, Assembly> _cadAsRef;

        private string _dllFile;

        private Dictionary<string, string> _loadedDllPaths;
        private Dictionary<string, LoadDllMessage> _loadYesList;

        /// <summary>
        ///     链式加载dll依赖
        /// </summary>
        /// <param name="dllFile"></param>
        public AssemblyDependent(string dllFile)
        {
            _dllFile = Path.GetFullPath(dllFile); //相对路径要先转换 Path.GetFullPath(dllFile);

            //cad程序集的依赖
            _cadAs = GetLoadeDictionary(AppDomain.CurrentDomain.GetAssemblies());

            //映射区
            _cadAsRef = GetLoadeDictionary(AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies());

            //被加载的都存放在这里
            _loadYesList = new Dictionary<string, LoadDllMessage>();
            _loadedDllPaths = new Dictionary<string, string>();
        }

        /// <summary>
        ///     加载DLL成功后获取到的程序集
        /// </summary>
        public List<Assembly> MyLoadAssemblys { get; private set; }


        //链条后面的不再理会,因为相同的dll引用辨识无意义
        /// <summary>
        ///     第一个dll加载是否成功
        /// </summary>
        public bool LoadOK { get; private set; }

        /// <summary>
        ///     加载出错信息
        /// </summary>
        public string LoadErrorMessage
        {
            get
            {
                var sb = new StringBuilder();
                var allyes = true;
                foreach (var item in _loadYesList)
                    if (!item.Value.LoadYes)
                    {
                        sb.Append(Environment.NewLine + "** 此文件已加载过,重复名称,重复版本号,本次不加载!");
                        sb.Append(Environment.NewLine + item);
                        sb.Append(Environment.NewLine);
                        allyes = false;
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
        ///     当前域加载事件,运行时出错的话,就靠这个事件来解决
        /// </summary>
        public event ResolveEventHandler CurrentDomainAssemblyResolveEvent
        {
            add => AppDomain.CurrentDomain.AssemblyResolve += value;
            remove => AppDomain.CurrentDomain.AssemblyResolve -= value;
        }

        public Dictionary<string, Assembly> GetLoadeDictionary(Assembly[] assemblies)
        {
            var dict = new Dictionary<string, Assembly>();
            foreach (var assembly in assemblies) dict[assembly.FullName] = assembly;
            return dict;
        }

        /// <summary>
        ///     加载程序集
        /// </summary>
        /// <param name="byteLoad">true字节加载,false文件加载</param>
        /// <returns>返回加载链的</returns>
        public void HotLoad(bool byteLoad = true)
        {
            _byteLoad = byteLoad;
            if (!File.Exists(_dllFile)) throw new ArgumentNullException("路径不存在");

            //查询加载链之后再逆向加载,确保前面不丢失
            var allRefs = GetAllRefPaths(_dllFile);
            allRefs.Reverse();

            foreach (var path in allRefs)
                try
                {
                    //路径转程序集名
                    var assName = AssemblyName.GetAssemblyName(path).FullName;

                    //路径转程序集名
                    //本次加载的dll不应提示版本号没变不加载
                    if (_cadAs.TryGetValue(assName, out var assembly) && !_loadYesList.TryGetValue(assName, out _))
                    {
                        _loadYesList[assName] = new LoadDllMessage(path, false); //版本号没变不加载
                        _loadedDllPaths[assName] = path;
                        continue;
                    }

                    byte[] buffer = null;
                    //实现字节加载
                    if (path == _dllFile) LoadOK = true;
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
                        var ass = AssemblyLoad<byte[]>(buffer, pdbbuffer);
                        DebugDelObjFiles(path);
                    }
#else
                    Assembly ass = null;
                    if (_byteLoad)
                    {
                        buffer = File.ReadAllBytes(path);
                        ass = AssemblyLoad<byte[]>(buffer);
                    }
                    else
                    {
                        ass = AssemblyLoad<string>(path);
                    }
#endif

                    _loadYesList[assName] = new LoadDllMessage(path, true); //加载成功
                    _loadedDllPaths[assName] = path;
                }
                catch
                {
                    _loadYesList["EXCEPTION"] = new LoadDllMessage(path, false); //错误造成
                }
        }

        /// <summary>
        ///     获取加载链
        /// </summary>
        /// <param name="dllFilePath"></param>
        /// <param name="refDllFilePaths"></param>
        /// <returns></returns>
        private List<string> GetAllRefPaths(string dllFilePath, List<string> refDllFilePaths = null)
        {
            if (refDllFilePaths == null) refDllFilePaths = new List<string>();
            if (refDllFilePaths.Contains(dllFilePath) || !File.Exists(dllFilePath)) return refDllFilePaths;
            refDllFilePaths.Add(dllFilePath);

            //路径转程序集名
            var assName = AssemblyName.GetAssemblyName(dllFilePath).FullName;

            //在当前程序域的assemblyAs内存区和assemblyAsRef映射区找这个程序集名
            ;
            Assembly assemblyAsRef;

            //内存区有表示加载过
            //映射区有表示查找过但没有加载(一般来说不存在.只是debug会注释掉Assembly.Load的时候用来测试)
            if (_cadAs.TryGetValue(assName, out var assemblyAs))
            {
                assemblyAsRef = assemblyAs;
            }
            else
            {
                //内存区和映射区都没有的话就把dll加载到内存区,用来找依赖表
                if (!_cadAsRef.TryGetValue(assName, out assemblyAsRef))
                {
                    // assemblyAsRef = Assembly.ReflectionOnlyLoad(dll); 没有依赖会直接报错
                    var byteRef = File.ReadAllBytes(dllFilePath);
                    //这里改掉原来的reflectiononlyoload因为会报错
                    assemblyAsRef = AssemblyLoad<byte[]>(byteRef);
                    _loadYesList[assName] = new LoadDllMessage(dllFilePath, true); //没有加载过
                    _loadedDllPaths[assName] = dllFilePath;
                }
            }

            //遍历依赖,如果存在dll拖拉加载目录就加入dlls集合
            foreach (var assemblyName in assemblyAsRef.GetReferencedAssemblies())
            {
                //dll拖拉加载路径-搜索路径(可以增加到这个dll下面的所有文件夹?)
                var directoryName = Path.GetDirectoryName(dllFilePath);

                var path = directoryName + "\\" + assemblyName.Name;
                var paths = new[]
                {
                    path + ".dll",
                    path + ".exe"
                };
                foreach (var patha in paths)
                    //前面加载过就不用加载了
                    if (!_loadedDllPaths.ContainsValue(patha))
                        GetAllRefPaths(patha, refDllFilePaths);
            }

            return refDllFilePaths;
        }

        private Assembly AssemblyLoad<T>(params T[] parameters)
        {
            var assemblyLoad = default(Assembly);
            try
            {
                if (parameters.Length == 2 && parameters[0] is byte[] buffer1 && parameters[1] is byte[] pdbbuffer)
                {
                    assemblyLoad = Assembly.Load(buffer1, pdbbuffer);
                }
                else if(parameters.Length==1)
                {
                    if (parameters[0] is byte[] buffer2) assemblyLoad = Assembly.Load(buffer2);
                    if (parameters[0] is string path) assemblyLoad = Assembly.LoadFile(path);
                }
                else
                {
                    return null;
                }
                _cadAs[assemblyLoad.FullName] = assemblyLoad;
            }
            // catch (Autodesk.AutoCAD.Runtime.Exception ex)
            // {
            //     //if (ex.ErrorStatus != ErrorStatus.DuplicateKey) throw;
            // }
            catch (System.Exception ex)
            {
                // Log or handle unexpected exceptions
                // Consider logging the exception or showing a message to understand what's happening
                Console.WriteLine($"Unexpected exception: {ex.Message}");
            }

            return assemblyLoad;
        }


        /// <summary>
        ///     递归删除文件夹目录及文件
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private static void DeleteFolder(string dir)
        {
            if (Directory.Exists(dir)) //如果存在这个文件夹删除之
            {
                foreach (var d in Directory.GetFileSystemEntries(dir))
                    if (File.Exists(d))
                        File.Delete(d); //直接删除其中的文件
                    else
                        DeleteFolder(d); //递归删除子文件夹
                Directory.Delete(dir, true); //删除已空文件夹
            }
        }

        /// <summary>
        ///     Debug的时候删除obj目录,防止占用
        /// </summary>
        public void DebugDelObjFiles(string dllFile)
        {
            try
            {
                //Delete pdb file in the same folder
                var filename = Path.GetFileNameWithoutExtension(dllFile);
                var path = Path.GetDirectoryName(dllFile);

                var strPdb = ".pdb";
                var pdb = path + "\\" + filename + strPdb;
                if (File.Exists(pdb)) File.Delete(pdb);
                //Delete pdb file in the  project folder of upper folder or the project folder under solution folder with same name
                //I am putting all output dll in the central path under solution folder above project folder.
                //Which cause the original code fail to remove pdb file in the project folder.
                var projectFolders = GetProjectFolder(dllFile);
                foreach (var projectFolder in projectFolders)
                {
                    var pdbFiles = Directory.GetFiles(projectFolder, $"*{strPdb}", SearchOption.AllDirectories);
                    if (pdbFiles.Length > 0)
                        foreach (var pdbFile in pdbFiles)
                            File.Delete(pdbFile);
                }
            }
            catch
            {
            }
        }

        public string GetParentFolderBySearchingFileExtension(string dllFilePath, string fileExtension)
        {
            var currentDirectory = Path.GetDirectoryName(dllFilePath);

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                var solutionFiles = Directory.GetFiles(currentDirectory, fileExtension);

                if (solutionFiles.Length > 0) return currentDirectory;

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            return null; // Solution folder not found
        }

        public IEnumerable<string> GetProjectFolder(string dllFilePath)
        {
            //get project folder if dll is in project subfolder
            var projectFolder = GetParentFolderBySearchingFileExtension(dllFilePath, FileExtensionCsProj);
            if (!string.IsNullOrEmpty(projectFolder)) return new[] { Directory.GetParent(projectFolder)?.FullName };
            //get project folder from the same name project folder in solution folder
            var solutionFolder = GetParentFolderBySearchingFileExtension(dllFilePath, FileExtensionSln);
            if (string.IsNullOrEmpty(solutionFolder)) return null;
            var projectFolders = Directory.GetFiles(solutionFolder,
                $"{Path.GetFileNameWithoutExtension(dllFilePath)}{FileExtensionCsProj}", SearchOption.AllDirectories);
            if (projectFolders.Length == 0) return null;
            return projectFolders.Select(p => Directory.GetParent(p)?.FullName);
        }

        /// <summary>
        ///     返回的类型,描述加载的错误
        /// </summary>
        public class LoadDllMessage
        {
            public bool LoadYes;
            public string Path;

            public LoadDllMessage(string path, bool loadYes)
            {
                Path = path;
                LoadYes = loadYes;
            }

            public override string ToString()
            {
                if (LoadYes) return "加载成功:" + Path;
                return "加载失败:" + Path;
            }
        }


        #region Dispose

        public bool Disposed;

        /// <summary>
        ///     显式调用Dispose方法,继承IDisposable
        /// </summary>
        public void Dispose()
        {
            //由手动释放
            Dispose(true);
            //通知垃圾回收机制不再调用终结器(析构器)_跑了这里就不会跑析构函数了
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     析构函数,以备忘记了显式调用Dispose方法
        /// </summary>
        ~AssemblyDependent()
        {
            //由系统释放
            Dispose(false);
        }


        /// <summary>
        ///     释放
        /// </summary>
        /// <param name="ing"></param>
        protected virtual void Dispose(bool ing)
        {
            if (Disposed)
                //不重复释放
                return;
            //让类型知道自己已经被释放
            Disposed = true;

            GC.Collect();
        }

        #endregion
    }
}