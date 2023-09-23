using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JoinBoxCurrency;
using RemoteAccess;

[assembly: CommandClass(typeof(HotLoadAcad.AcadHot_loading))]

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
    class AcadHot_loading
    {
        [CommandMethod("HotLoading_dll")]
        public void MainLoading()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "dll|*.dll";           //删选、设定文件显示类型
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string path = ofd.FileName;  //获得选择的文件路径

            var ad = new AssemblyDependent(path);
            //运行时出错的话,就靠这个事件来解决
            ad.CurrentDomainAssemblyResolveEvent += RunTimeCurrentDomain.DefaultAssemblyResolve;

            ad.Load();
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage(ad.LoadErrorMessage);
        }
    }
}