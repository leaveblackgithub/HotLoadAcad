# HotLoadAcad
from 《CAD.net开发动态加载dll》 https://zhuanlan.zhihu.com/p/398811473
# How to use
from 《CAD.net开发链式加载DLL调试方法》 https://zhuanlan.zhihu.com/p/398817502
# NOTE
迭代版本号
必须更改版本号最后是*,否则无法重复加载(所有)
如果想加载时候动态修改dll的版本号,需要学习PE读写.(此文略)

net framework要直接编辑项目文件.csproj,启用由vs迭代版本号:

<PropertyGroup>
  <Deterministic>False</Deterministic>
</PropertyGroup>
![v2-9e6c445d565fdc4a323d5402de14658b_r](https://github.com/user-attachments/assets/98c0f239-3f3b-430e-82fd-ea69d9796509)
