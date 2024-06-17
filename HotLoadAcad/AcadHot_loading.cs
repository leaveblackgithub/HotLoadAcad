using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HotLoadAcad;
using JoinBoxCurrency;
using RemoteAccess;
using AcadApp = Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(AcadHot_loading))]

namespace HotLoadAcad
{
    /*  net standard只需要增加.csproj的这里,没有自己加一个:
     *  <PropertyGroup>
     *  <AssemblyVersion>1.0.0.*</AssemblyVersion> 
     *  <FileVersion>1.0.0.0</FileVersion>
     *  <Deterministic>False</Deterministic>
     *  </PropertyGroup>
     *  
     *  net framework要直接编辑项目文件.csproj,启用由vs迭代版本号
     *  <PropertyGroup><Deterministic>False</Deterministic></PropertyGroup>
     *  [assembly: AssemblyVersion("1.0.*")]
     */
    internal class AcadHot_loading
    {
        private static  Editor ActiveEditor => Application.DocumentManager.MdiActiveDocument.Editor;

        [CommandMethod("HotLoading_dll")]
        public void MainLoading()
        {
            ActiveEditor.WriteMessage("HotLoading_dll240124\n");
            var path = (Application.GetSystemVariable("FILEDIA").ToString()=="0")? GetPathFrEditor() : GetPathFrDia();
            if (path == "") return;
            var ad = new AssemblyDependent(path);
            //运行时出错的话,就靠这个事件来解决
            ad.CurrentDomainAssemblyResolveEvent += RunTimeCurrentDomain.DefaultAssemblyResolve;

            ad.HotLoad();
            var ed = ActiveEditor;
            ed.WriteMessage(ad.LoadErrorMessage);
        }

        private static string GetPathFrDia()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "dll|*.dll"; //删选、设定文件显示类型
            if (ofd.ShowDialog() != DialogResult.OK)
                return "";

            var path = ofd.FileName; //获得选择的文件路径
            return path;
        }

        private static string GetPathFrEditor()
        {
            var ed = ActiveEditor;
            var pr = ed.GetString("输入dll路径:");
            if (pr.Status != PromptStatus.OK)
                return "";

            var path = pr.StringResult; //获得选择的文件路径
            return path;
        }
    }
}