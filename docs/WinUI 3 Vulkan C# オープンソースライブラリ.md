# **WinUI 3環境における高パフォーマンスVulkanレンダリングエンジンのオープンソース実装戦略に関する包括的調査報告書**

## **1\. エグゼクティブサマリーと戦略的展望**

現代のWindowsアプリケーション開発において、WinUI 3（Windows UI Library 3）はモダンで流麗なユーザーインターフェースを構築するための標準的なフレームワークとして位置づけられています。しかし、高度な3Dグラフィックス、特にクロスプラットフォーム対応と高パフォーマンスを特徴とするVulkan APIをWinUI 3内で利用しようとした場合、開発者は深刻な「エコシステムの断絶」に直面します。

現状の.NET 3Dエコシステムは、歴史的な経緯からDirectX（Direct3D 11/12）を中心に形成されています。代表的なオープンソースライブラリであるHelix ToolkitはDirectXに深く依存しており、Vulkanのサポートは実験的な段階に留まっています。一方で、VulkanとWinUI 3の統合を商用レベルで実現しているAb4d.SharpEngineのようなソリューションは存在しますが、これらは高額なライセンス費用を伴うプロプライエタリ製品であり、初期投資を抑えたい商用プロジェクト、特に「販売用アプリケーション」を開発する個人や小規模チームにとっては採用が困難です1。

本報告書は、**「商用利用が可能かつ無償（オープンソース）」であり、なおかつ「WinUI 3上でVulkanを使用し、任意のジェスチャーで3Dファイルを描画する」という要件を満たすための技術的解を網羅的に調査・検証したものです。結論として、既存の「完成された」オープンソースのVulkan-WinUI 3専用コントロール（WPFのHelixToolkit.Wpfのようなもの）は現時点で存在しません。しかし、Silk.NETやDiligent Engine**といった低レイヤー/中間レイヤーのライブラリを基盤とし、DirectXとVulkanの相互運用（Interop）アーキテクチャを独自に実装することで、商用エンジンの購入を回避しつつ、同等の機能を実装することは十分に可能です。

本稿では、この「独自実装によるコスト回避」を前提とした、具体的かつ包括的なアーキテクチャ設計、ライブラリ選定、および実装ロードマップを提示します。これにより、ライセンス費用という「金銭的税」を、技術的な理解と実装という「労力的投資」に置き換え、永続的な知的財産としてのレンダリング基盤を構築する道筋を示します。

## **2\. 現行の技術ランドスケープと課題分析**

### **2.1. WinUI 3における3Dレンダリングの制約**

WinUI 3は、その描画パイプラインの深層においてDirectX（具体的にはDirectCompositionおよびDirect2D/3D）と密接に結合しています。WPFのD3DImageやUWPのSwapChainPanelと同様に、WinUI 3が画面にピクセルを表示するためには、最終的にDXGI（DirectX Graphics Infrastructure）のスワップチェーンを経由する必要があります4。

Vulkanはプラットフォームに依存しないAPIであるため、Windows固有のDXGIスワップチェーンを直接操作することは設計思想に含まれていません。ここに「Vulkanで描画した結果を、いかにしてDirectXベースのWinUI 3コントロールに渡すか」という根本的な技術的障壁が存在します。Ab4dのような商用ライブラリは、この複雑なブリッジ処理を内部にカプセル化することで価値を提供していますが、オープンソースのみでこれを実現するには、このブリッジ部分を開発者自身が制御する必要があります1。

### **2.2. 既存ライブラリの評価と除外理由**

市場に存在する主要な.NET向け3Dライブラリを、本プロジェクトの要件（WinUI 3, Vulkan, 商用無料, 3Dファイル表示, ジェスチャー操作）に照らして評価しました。

| ライブラリ名 | ライセンス | WinUI 3対応 | Vulkan対応 | 3Dファイル読込 | 判定 |
| :---- | :---- | :---- | :---- | :---- | :---- |
| **Ab4d.SharpEngine** | 商用 (有償) | ◎ (ネイティブ) | ◎ (ネイティブ) | ◎ (Assimp内蔵) | **除外** (コスト要件不適合) 1 |
| **Helix Toolkit** | MIT (OSS) | ◯ (SharpDX) | △ (実験的/Nex) | ◎ (Assimp) | **不適合** (Vulkanサポートが未成熟) 3 |
| **Veldrid** | MIT (OSS) | △ (実装可) | ◯ | ✕ (別途必要) | **非推奨** (メンテナ更新停止リスク) 6 |
| **Silk.NET** | MIT (OSS) | △ (要実装) | ◎ (Binding) | ◎ (Assimp) | **推奨** (完全な制御と将来性) 8 |
| **Diligent Engine** | Apache 2.0 | △ (要実装) | ◎ (Abstraction) | ◎ (GLTFLoader) | **推奨** (生産性と機能のバランス) 10 |

**分析結果:**

* **Helix Toolkit:** WinUI 3向けの実装（HelixToolkit.WinUI.SharpDX）は存在しますが、バックエンドはDirectX 11に依存しており、SharpDX自体の開発終了に伴うメンテナンスモードへの移行が懸念されます。次世代のHelixToolkit.NexでVulkan対応が進められていますが、現時点で商用製品の基盤とするにはリスクが高い状態です3。  
* **Veldrid:** かつては有力な候補でしたが、2023年以降メインテナが更新停止を宣言しており、.NET 8/9への適合性や将来のセキュリティパッチの観点から、新規の商用プロジェクトでの採用は推奨されません6。

したがって、解決策は**Silk.NET**または**Diligent Engine**を用いて、独自のビューポートコントロールを構築することに集約されます。

## **3\. 推奨ライブラリの詳細評価**

### **3.1. Silk.NET：究極の制御とパフォーマンス**

ライセンス: MIT  
ステータス:.NET Foundation プロジェクト、活発に開発中  
Silk.NETは、OpenTKの精神的後継であり、Vulkan、DirectX、OpenGL、OpenALなどのネイティブAPIに対する高速で薄いラッパー（Bindings）を提供します。これは「エンジン」ではなく「バインディング」であるため、APIの生の機能をそのままC\#から呼び出すことができます。

* **適合性:**  
  * **Vulkan:** 最新のVulkanヘッダーに対するバインディングが自動生成されており、VK\_KHR\_external\_memory\_win32などの拡張機能もフルサポートされています。WinUI 3との連携に不可欠な低レベル制御が可能です8。  
  * **3Dファイル:** Silk.NET.Assimpパッケージを提供しており、数十種類の3Dフォーマット（GLTF, OBJ, FBX等）の読み込みをサポートしています。これにより「3Dファイルを描画したい」という要件を直接満たします12。  
  * **WinUI 3:** 直接的なWinUIコントロールは提供していませんが、DirectXバインディングも含んでいるため、相互運用コードを同一ライブラリ内で完結させることが可能です。

### **3.2. Diligent Engine：生産性を高める抽象化レイヤー**

ライセンス: Apache 2.0  
ステータス: 商用利用実績多数、継続的なアップデート  
Diligent Engineは、DirectX 12とVulkanの複雑さを隠蔽し、統一されたAPIを提供するクロスプラットフォームグラフィックスライブラリです。C++で記述されていますが、公式の.NETバインディングが提供されています。

* **適合性:**  
  * **Vulkan:** バックエンドとしてVulkanを強力にサポートしており、リソースの状態管理（Resource State Transition）やメモリバリアといったVulkan特有の難解な処理を自動化してくれます13。  
  * **3Dファイル:** サンプルプロジェクト内にGLTF 2.0ローダーとPBR（物理ベースレンダリング）の実装が含まれており、これを流用することで高品質な描画を即座に実現できます10。  
  * **WinUI 3:** ユニバーサルWindowsプラットフォーム（UWP）およびWin32との連携実績があり、WinUI 3への適応も、ウィンドウハンドルの受け渡しレベルで可能です10。

### **3.3. 選定の結論**

「完全にゼロから構築し、ブラックボックスを排除したい」場合はSilk.NETが最適です。  
「Vulkanの煩雑なボイラープレートコード（初期化や同期）を削減し、レンダリングロジックに集中したい」場合はDiligent Engineが推奨されます。本報告書では、より汎用的かつ学習リソースとしての価値が高い、Silk.NETを用いた「相互運用アーキテクチャ」を中心に解説しますが、概念はDiligent Engineでも応用可能です。

## **4\. アーキテクチャ詳細：WinUI 3とVulkanの相互運用（Interop）**

本プロジェクトの核心は、Vulkanで描画されたフレームを、いかにして遅延なくWinUI 3のSwapChainPanelに転送するかという点にあります。CPU経由のメモリコピー（ReadPixelsなど）はパフォーマンスを著しく低下させるため、GPUメモリ共有（Zero-Copy）技術を用います。

### **4.1. 共有リソース（Shared Handle）メカニズム**

WinUI 3アプリはDirectXの世界で生きています。Vulkanの映像を表示するためには、DirectXとVulkanの両方がアクセス可能な「共有テクスチャ」を作成し、Vulkanが書き込み、DirectXがそれを読み取って表示するというパイプラインを構築します。

具体的なデータフローは以下の通りです。

1. **DirectX側（WinUIスレッド）:**  
   * DXGIスワップチェーンを作成し、SwapChainPanelに関連付けます。  
   * 共有用のD3D11テクスチャ（バックバッファとは別）を作成します。この際、ResourceOptionFlags.Sharedフラグを設定します。  
   * このテクスチャから、OSレベルの共有ハンドル（HANDLE / IntPtr）を取得します5。  
2. **Vulkan側（レンダリングスレッド）:**  
   * Vulkanインスタンスとデバイスの初期化時に、VK\_KHR\_external\_memory\_win32およびVK\_KHR\_win32\_surface拡張機能を有効にします。  
   * vkGetMemoryWin32HandleKHR（またはインポート関数）を使用して、DirectX側で作成した共有ハンドルをVulkanのメモリ空間にインポートします。  
   * このインポートされたメモリにバインドされたVkImageを作成します。これがVulkanのレンダリングターゲット（フレームバッファのアタッチメント）となります5。  
3. **同期（Synchronization）:**  
   * Vulkanでの描画完了とDirectXでの表示開始が競合しないよう、KeyedMutex（キー付きミューテックス）またはSemaphore（セマフォ）を用いた同期が必要です。通常は、Vulkan側で描画完了後にセマフォをシグナルし、DirectX側でそのセマフォを待機するか、より堅牢なIDXGIKeyedMutexを使用します5。

### **4.2. ISwapChainPanelNativeへのアクセス**

WinUI 3のSwapChainPanelはXAMLコントロールですが、その裏側にはCOMインターフェースISwapChainPanelNativeが存在します。C\#からこれにアクセスするには、COM相互運用ライブラリが必要です。

* **推奨ライブラリ:** Vortice.Windows または TerraFX.Interop.Windows  
* **実装手順:**  
  1. XAMLで定義したSwapChainPanelインスタンスをIInspectable（またはIUnknown）にキャストします。  
  2. QueryInterfaceメソッドを用いて、ISwapChainPanelNativeインターフェースへのポインタを取得します。  
  3. 取得したインターフェースのSetSwapChainメソッドを呼び出し、DirectX側で作成したDXGIスワップチェーンを渡します4。

これにより、Vulkanが共有テクスチャに描画 → DirectXが共有テクスチャからバックバッファにコピー → スワップチェーンがWinUIパネルに表示、という高速なパスが開通します。

## **5\. 「任意のジェスチャー」を実現する入力系とカメラ制御**

ユーザー要件にある「任意のジェスチャー」とは、具体的にはマウスやタッチ操作による回転（Orbit）、パン（Pan）、ズーム（Zoom）を指すと推測されます。ゲームエンジンを使用しない場合、これらの相互作用ロジックは自前で実装する必要があります。

### **5.1. WinUI 3のポインターイベント**

WinUI 3は、マウス、タッチ、ペンを抽象化した「ポインターイベント」を提供します。SwapChainPanel上でこれらのイベントを購読することで、デバイスに依存しないジェスチャー処理が可能になります。

| イベント名 | 役割 | 実装ロジック |
| :---- | :---- | :---- |
| **PointerPressed** | 操作開始 | 現在の座標を記録。操作モード（回転/移動）を判定。CapturePointerでポインタをキャプチャし、パネル外への移動を追跡可能にする17。 |
| **PointerMoved** | 操作中 | 直前の座標との差分（Delta）を計算。差分値をカメラコントローラーに渡してView行列を更新19。 |
| **PointerReleased** | 操作終了 | ポインタのキャプチャを解放。操作モードをリセット。 |
| **PointerWheelChanged** | ズーム | ホイールのデルタ値を取得し、カメラの距離（Distance）パラメータを加減算20。 |

### **5.2. ArcBall（アークボール）カメラの数学的実装**

「任意のジェスチャーで3Dファイルを見る」ための標準的なカメラ操作モデルは**ArcBall**です。これは画面上の2D操作を、3Dオブジェクトを取り囲む仮想球体上の回転に変換する手法です。

**実装アルゴリズム:**

1. **座標変換:** スクリーン座標 $(x, y)$ を、画面中心を原点とする正規化デバイス座標（NDC: \-1.0 ～ 1.0）に変換します。  
2. 球体への投影: 2D座標を仮想球体上の3Dベクトル $P$ に投影します。

   $$P.z \= \\sqrt{1 \- x^2 \- y^2} \\quad (x^2 \+ y^2 \\le 1 \\text{の場合})$$  
3. クォータニオン回転: クリック開始点のベクトル $v\_{start}$ と現在の点のベクトル $v\_{current}$ の間の回転を表すクォータニオン $q$ を計算します。

   $$q \= \\text{Quaternion.RotationBetween}(v\_{start}, v\_{current})$$  
4. **行列更新:** 現在のカメラの回転クォータニオンに $q$ を乗算し、新しいView行列（Matrix4x4.CreateLookAt）を生成します21。

この計算には、.NET標準のSystem.Numericsライブラリを使用します。これにより、外部の数学ライブラリに依存することなく、高精度なカメラ制御が可能になります。

## **6\. 3Dファイルの読み込みとアセットパイプライン**

「3Dファイル（GLTF, OBJ等）を表示する」という要件に対しては、**Open Asset Import Library (Assimp)** の利用が業界標準です。

### **6.1. Silk.NET.Assimpの統合**

Silk.NETにはSilk.NET.Assimpという公式バインディングが含まれています。これを利用することで、C++のAssimpライブラリを直接呼び出し、多様なフォーマットの3Dデータをインポートできます。

**処理フロー:**

1. **インポート:** Assimp.ImportFileを呼び出し、PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs などのフラグを指定して、レンダリングしやすい形式（三角ポリゴン化、UV座標反転など）に正規化します。  
2. **データ抽出:** SceneオブジェクトからMeshリストを走査し、頂点座標（Vertices）、法線（Normals）、インデックス（Indices）データを配列として抽出します。  
3. **Vulkanリソース化:** 抽出した配列データを、VulkanのVkBuffer（Staging Buffer経由でDevice Local Memory）に転送します。  
4. **描画:** コマンドバッファの記録時に、これらのバッファをバインドしてvkCmdDrawIndexedを発行します12。

このパイプラインを構築することで、特定のファイル形式に依存せず、Assimpがサポートするあらゆる3Dファイルをアプリケーションで表示可能になります。

## **7\. 実装ロードマップ：ゼロからのエンジン構築**

予算ゼロで商用レベルのビューアーを実現するための、具体的かつ現実的な開発ステップを提示します。

### **フェーズ1：土台の構築（The Foundation）**

* **目標:** WinUI 3ウィンドウに、Vulkanで塗りつぶした単色（Clear Color）を表示する。  
* **タスク:**  
  * WinUI 3プロジェクトの作成。  
  * DirectX 11デバイスとDXGIスワップチェーンの作成。  
  * ISwapChainPanelNativeを用いたスワップチェーンの接続。  
  * Vulkanインスタンスの初期化と共有ハンドルのインポート。  
  * 描画ループ（CompositionTarget.Rendering または DispatcherQueue）の実装。

### **フェーズ2：アセットの表示（The Asset）**

* **目標:** Assimpを使って読み込んだ3Dモデル（例：立方体やティーポット）を画面に表示する。  
* **タスク:**  
  * Silk.NET.Assimpの導入。  
  * 頂点バッファ、インデックスバッファの作成ロジックの実装。  
  * SPIR-Vシェーダー（頂点・フラグメント）のコンパイルとパイプラインの作成。  
  * 深度バッファ（Depth Buffer）の設定（3D表示に必須）。

### **フェーズ3：インタラクション（The Interaction）**

* **目標:** マウス操作でモデルを回転・ズームできるようにする。  
* **タスク:**  
  * PointerPressed, PointerMoved, PointerWheelChangedイベントの実装。  
  * ArcBallカメラクラス（System.Numerics利用）の作成。  
  * View行列、Projection行列をUniform Buffer経由でシェーダーに渡す処理の実装。

### **フェーズ4：最適化と堅牢化（Optimization）**

* **目標:** リサイズ時のチラつき防止とハイDPI対応。  
* **タスク:**  
  * CompositionScaleChangedイベントのハンドリング（DPIスケーリング対応）。  
  * ウィンドウリサイズ時のスワップチェーンおよびVulkanイメージの再生成処理。  
  * 適切なリソース解放（IDisposableパターンの徹底）によるメモリリーク防止。

## **8\. 結論**

「VulkanをWinUI 3で使用し、追加費用なしで商用アプリケーションを開発したい」という要望に対する答えは、\*\*「Silk.NETまたはDiligent Engineを核とし、DirectX-Vulkan相互運用レイヤーを自社実装する」\*\*ことにあります。

市場には「これを導入すれば終わり」という無料のWinUI 3専用Vulkanコントロールは存在しません。しかし、本報告書で詳述した相互運用アーキテクチャ（Shared Handleパターン）と、標準的な数学ライブラリによるカメラ制御、そしてAssimpによるアセット読み込みを組み合わせることで、高額な商用エンジンに匹敵する機能を、ライセンス料フリー（MIT/Apache 2.0）で実現することは技術的に十分可能です。

このアプローチは初期の実装コスト（学習とコーディング）を必要としますが、その対価として、アプリケーションのレンダリングパイプラインに対する完全な制御権と、将来にわたるライセンス費用の削減という永続的な利益をもたらします。

### ---

**付録：主要構成要素比較表**

| コンポーネント | 推奨ライブラリ | ライセンス | 役割・機能 |
| :---- | :---- | :---- | :---- |
| **APIバインディング** | **Silk.NET** | MIT | C\#からVulkanおよびDirectX APIへのアクセスを提供。エンジンの核となる8。 |
| **モデルローダー** | **Silk.NET.Assimp** | BSD-3-Clause | GLTF, OBJ, FBX等の3Dファイル解析とデータ抽出を担当12。 |
| **数学・計算** | **System.Numerics** | MIT (.NET標準) | カメラ制御（ArcBall）に必要なベクトル・行列・クォータニオン演算を提供25。 |
| **WinUI相互運用** | **TerraFX / Vortice** | MIT | ISwapChainPanelNativeなどのCOMインターフェース定義を提供15。 |
| **UIフレームワーク** | **WinUI 3** | Proprietary (MS) | アプリケーションのシェル、2D UI、入力イベントのハンドリング26。 |

#### **引用文献**

1. Cross-platform Vulkan based 3D rendering engine for .Net Apps \- Ab4d.SharpEngine, 12月 18, 2025にアクセス、 [https://www.ab4d.com/SharpEngine.aspx](https://www.ab4d.com/SharpEngine.aspx)  
2. c\# 3d graphics lib : r/csharp \- Reddit, 12月 18, 2025にアクセス、 [https://www.reddit.com/r/csharp/comments/17cdt3b/c\_3d\_graphics\_lib/](https://www.reddit.com/r/csharp/comments/17cdt3b/c_3d_graphics_lib/)  
3. Helix Toolkit, 12月 18, 2025にアクセス、 [https://helix-toolkit.github.io/](https://helix-toolkit.github.io/)  
4. SwapChainPanel Class (Microsoft.UI.Xaml.Controls) \- Windows App SDK, 12月 18, 2025にアクセス、 [https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.swapchainpanel?view=windows-app-sdk-1.8](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.swapchainpanel?view=windows-app-sdk-1.8)  
5. malstraem/vulkan-interop-directx: How to render into ... \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/malstraem/vulkan-interop-directx](https://github.com/malstraem/vulkan-interop-directx)  
6. veldrid/veldrid: A low-level, portable graphics library for .NET. \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/veldrid/veldrid](https://github.com/veldrid/veldrid)  
7. veldrid/README.md at master \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/veldrid/veldrid/blob/master/README.md](https://github.com/veldrid/veldrid/blob/master/README.md)  
8. Silk.NET \- Bringing Vulkan, OpenGL, OpenCL, OpenAL, and GLFW to C\# : r/csharp \- Reddit, 12月 18, 2025にアクセス、 [https://www.reddit.com/r/csharp/comments/fiiab3/silknet\_bringing\_vulkan\_opengl\_opencl\_openal\_and/](https://www.reddit.com/r/csharp/comments/fiiab3/silknet_bringing_vulkan_opengl_opencl_openal_and/)  
9. Package Silk.NET.Assimp \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/orgs/dotnet/packages/nuget/package/Silk.NET.Assimp](https://github.com/orgs/dotnet/packages/nuget/package/Silk.NET.Assimp)  
10. DiligentGraphics/DiligentEngine: A modern cross-platform low-level graphics library and rendering framework \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/DiligentGraphics/DiligentEngine](https://github.com/DiligentGraphics/DiligentEngine)  
11. Announcing Silk.NET 2.17: Cleaner Code, Smoother Experience., 12月 18, 2025にアクセス、 [https://dotnet.github.io/Silk.NET/blog/apr-2023/silk2170/](https://dotnet.github.io/Silk.NET/blog/apr-2023/silk2170/)  
12. Silk.NET.Assimp 2.22.0 \- NuGet, 12月 18, 2025にアクセス、 [https://www.nuget.org/packages/Silk.NET.Assimp/](https://www.nuget.org/packages/Silk.NET.Assimp/)  
13. Diligent Engine \- Diligent Graphics, 12月 18, 2025にアクセス、 [https://diligentgraphics.com/diligent-engine/](https://diligentgraphics.com/diligent-engine/)  
14. Samples and Tutorials \- Diligent Graphics, 12月 18, 2025にアクセス、 [http://diligentgraphics.com/diligent-engine/samples/](http://diligentgraphics.com/diligent-engine/samples/)  
15. Add support for creating swapchains on WinUI 3 based applications by ollitanska · Pull Request \#416 \- GitHub, 12月 18, 2025にアクセス、 [https://github.com/mellinoe/veldrid/pull/416](https://github.com/mellinoe/veldrid/pull/416)  
16. Creating DirectX 11 SwapChain in WinUI 3.0, 12月 18, 2025にアクセス、 [https://juhakeranen.com/winui3/directx-11-2-swap-chain.html](https://juhakeranen.com/winui3/directx-11-2-swap-chain.html)  
17. UIElement.PointerEntered Event (Microsoft.UI.Xaml) \- Windows App SDK, 12月 18, 2025にアクセス、 [https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointerentered?view=windows-app-sdk-1.8](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointerentered?view=windows-app-sdk-1.8)  
18. Pointer Events were not triggered in WinUi3 \- Stack Overflow, 12月 18, 2025にアクセス、 [https://stackoverflow.com/questions/79410931/pointer-events-were-not-triggered-in-winui3](https://stackoverflow.com/questions/79410931/pointer-events-were-not-triggered-in-winui3)  
19. Handle pointer input \- Windows apps | Microsoft Learn, 12月 18, 2025にアクセス、 [https://learn.microsoft.com/en-us/windows/apps/develop/input/handle-pointer-input](https://learn.microsoft.com/en-us/windows/apps/develop/input/handle-pointer-input)  
20. C\# WoW-esque arcball camera code, debug help please? : r/Unity3D \- Reddit, 12月 18, 2025にアクセス、 [https://www.reddit.com/r/Unity3D/comments/6r8ydh/c\_wowesque\_arcball\_camera\_code\_debug\_help\_please/](https://www.reddit.com/r/Unity3D/comments/6r8ydh/c_wowesque_arcball_camera_code_debug_help_please/)  
21. Arcball Implementation Dependency on View Plane \- Stack Overflow, 12月 18, 2025にアクセス、 [https://stackoverflow.com/questions/43130629/arcball-implementation-dependency-on-view-plane](https://stackoverflow.com/questions/43130629/arcball-implementation-dependency-on-view-plane)  
22. 3.1 Arcball Camera Control \- WebGPU Unleashed: A Practical Tutorial, 12月 18, 2025にアクセス、 [https://shi-yan.github.io/webgpuunleashed/Control/arcball\_camera\_control.html](https://shi-yan.github.io/webgpuunleashed/Control/arcball_camera_control.html)  
23. Maths for various camera movement ( Orbit, Arcball, FPS, Zoom, etc ) \- GitHub Gist, 12月 18, 2025にアクセス、 [https://gist.github.com/sketchpunk/8fe68c6c2e4d64a479880c3596e95811](https://gist.github.com/sketchpunk/8fe68c6c2e4d64a479880c3596e95811)  
24. Hello Window | Silk.NET, 12月 18, 2025にアクセス、 [https://dotnet.github.io/Silk.NET/docs/opengl/c1/1-hello-window/](https://dotnet.github.io/Silk.NET/docs/opengl/c1/1-hello-window/)  
25. 3D Rotation using System.Numerics.Quaternion \- Stack Overflow, 12月 18, 2025にアクセス、 [https://stackoverflow.com/questions/57185542/3d-rotation-using-system-numerics-quaternion](https://stackoverflow.com/questions/57185542/3d-rotation-using-system-numerics-quaternion)  
26. WinUI 3 \- Windows apps | Microsoft Learn, 12月 18, 2025にアクセス、 [https://learn.microsoft.com/en-us/windows/apps/winui/winui3/](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)