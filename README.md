# HotLoadAcad
from 《CAD.net开发动态加载dll》 https://zhuanlan.zhihu.com/p/398811473
# How to use
from 《CAD.net开发链式加载DLL调试方法》 https://zhuanlan.zhihu.com/p/398817502
# NOTE
迭代版本号
必须更改版本号最后是*,否则无法重复加载(所有)
如果想加载时候动态修改dll的版本号,需要学习PE读写.(此文略)

- net framework要直接编辑项目文件.csproj,启用由vs迭代版本号:

'''xml
<PropertyGroup>
  <Deterministic>False</Deterministic>
</PropertyGroup>
'''

同时修改AssemblyInfo.cs
'''xml
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.0.0")]
'''

- net standard只需要增加.csproj的这里,没有自己加一个:

'''xml
<PropertyGroup>
    <AssemblyVersion>1.0.0.*</AssemblyVersion> 
    <FileVersion>1.0.0.0</FileVersion>
    <Deterministic>False</Deterministic>
</PropertyGroup>
'''
