# [WPF] GitHub Actions を使った自動リリース

2023.1.5 新規作成



## はじめに

正月休みを利用して 個人開発の .NET8 WPFアプリを GitHub Actions で自動リリースする仕組みを作りました。

n番煎じの記事となりますが、皆様の記事を読んでも詰まるところがありましたので、改めてまとめました。（微妙にやりたいことが違ったり、内容が古くて warning が出たり）

一連のソフトは以下に公開しています。

[hsytkm/WpfReleaseActionDemo: Release WPF app using GitHub Actions.](https://github.com/hsytkm/WpfReleaseActionDemo)



## ゴール

WPFアプリの **自己完結型の単一ファイル(.exe)** の zip を GitHub Releases に登録する作業 を自動化します。（現状は手作業が面倒なのでメジャーバージョン以外では対応できていません。）

ちなみに、GitHub が リポジトリのトップページでサジェストしてくる `.NET Desktop` のワークフローは MSIXパッケージ を想定しているので、今回のゴールと微妙に差があります。



## 前提

### GitHub Actions の課金 (2024年1月時点)

Public / Private リポジトリで差がありますが、月単位で一定時間まで無料で利用できます。 また、無料枠を使い切っても突然費用が発生することはないそうなので安心です。

[GitHub Actions の課金について - GitHub Docs](https://docs.github.com/ja/billing/managing-billing-for-github-actions/about-billing-for-github-actions)



私の場合は、本記事のため Private リポジトリで試行錯誤して 約320分 (15%) を消費しました。 個人で安定して数アプリを運用する程度なら無料枠を使い切ることはなさそうです。

利用状況は以下のページから確認できます。

> GitHub → Settings → Billing and plans → Plans and usage → Usage this month → Actions

～～画像～～

### ソリューションの構成

GitHub Actions のテスト用にシンプルな構成の WPF ソリューションを用意しました。

1. WpfReleaseAction.App : WPFアプリ本体
2. WpfReleaseAction.Model : WPFアプリから利用するプロジェクト（複数プロジェクトをビルドするテスト用）
3. WpfReleaseAction.Tests : テストプロジェクト（自動テスト用）

MSIXパッケージではないので `Windows アプリケーション パッケージ プロジェクト (*.wapproj)` は用意していません。



## Actions の作成

いよいよ本題です。

### 1. Actions の権限変更

GitHub Actions が生成した zip を Releases に登録するため、先に Actions の権限を変更しておきます。

1. リポジトリ → Settings → Actions→ General に移動します。
2. `Workflow permissions` の設定を `Read repository contents and packages permissions` から `Read and write permissions` に変更します。

※MSIXパッケージでなければ `Actions Secrets` の登録は必要ありません。

Write 権限を有効にしていない場合、zip ファイルを Releases に登録する際に  `Error 403: Resource not accessible by integration` が出ます

### 2. YAMLファイルの追加

先に YAML ファイルの全体を紹介しておきます。 `.\.github\workflows\build\cicd.yml` と同じ内容です。

```yaml
name: .NET Build and Test
on:
  push:

env:
  App_Name: WpfReleaseActionDemo
  Solution_Path: WpfReleaseActionDemo.sln
  App_Project_Path: src/WpfDemo.App/WpfDemo.App.csproj

jobs:
  build:
    strategy:
        matrix:
          configuration: [Release]  # [Debug, Release]
    runs-on: windows-latest
    timeout-minutes: 15

    steps:
      # Dump for debug workflow
      - name: Dump Github Context
        env:
          GitHub_Context: ${{ toJson(github) }}
        run: echo "${GitHub_Context}"

      # Checks-out repository under $GITHUB_WORKSPACE: https://github.com/actions/checkout
      - name: Checkout
        uses: actions/checkout@v3
        with:
          fetch-depth: 0

      # Install the .NET workload: https://github.com/actions/setup-dotnet
      - name: Install .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x

      # Add MsBuild to the PATH: https://github.com/microsoft/setup-msbuild
      - name: Setup MSBuild.exe
        uses: microsoft/setup-msbuild@v1.3.1

      # Restore before build and test
      - name: Restore
        run: dotnet restore ${{ env.Solution_Path }}

      - name: Build with dotnet
        run: dotnet build ${{ env.App_Project_Path }} --no-restore
        env:
          Configuration: ${{ matrix.configuration }}

      # Execute all unit tests in the solution
      - name: Execute unit tests
        run: dotnet test --no-restore

  create-release:
    runs-on: windows-latest
    timeout-minutes: 15
    needs: [build]
    if: "contains( github.ref , 'tags/v')"

    steps:
      - name: echos
        shell: bash
        run: |
          echo $RELEASE_VERSION
          echo "version=${GITHUB_REF/refs\/tags\/v/}" >> $GITHUB_ENV
          echo "app_x64_framework_name=${{ env.App_Name }}_win-x64_framework-dependent_ver${GITHUB_REF/refs\/tags\/v/}" >> $GITHUB_ENV
          echo "app_x64_self_name=${{ env.App_Name }}_win-x64_self-contained_ver${GITHUB_REF/refs\/tags\/v/}" >> $GITHUB_ENV

      # Checks-out repository under $GITHUB_WORKSPACE: https://github.com/actions/checkout
      - name: Checkout
        uses: actions/checkout@v3
        with:
          fetch-depth: 0

      - name: dotnet publish x64 Framework-dependent
        run: >
          dotnet publish ${{ env.App_Project_Path }}
          -c Release
          -r win-x64
          --self-contained false -p:UseAppHost=true
          -p:PublishSingleFile=true
          -p:PublishReadyToRun=false
          -p:PublishTrimmed=false
          -p:IncludeNativeLibrariesForSelfExtract=true
          -o outputs\${{ env.app_x64_framework_name }}

      - name: dotnet publish x64 Self-contained
        run: >
          dotnet publish ${{ env.App_Project_Path }}
          -c Release
          -r win-x64
          --self-contained true
          -p:PublishSingleFile=true
          -p:PublishReadyToRun=false
          -p:PublishTrimmed=false
          -p:IncludeNativeLibrariesForSelfExtract=true
          -o outputs\${{ env.app_x64_self_name }}

      # Upload Actions Artifacts: https://github.com/actions/upload-artifact
      - name: Archive publish files
        uses: actions/upload-artifact@v3
        with:
          name: ${{ env.App_Name }}
          path: outputs

      # Create zip
      - name: Create zip archive
        shell: pwsh
        run: |
          Compress-Archive -Path outputs\${{ env.app_x64_framework_name }} -DestinationPath ${{ env.app_x64_framework_name }}.zip
          Compress-Archive -Path outputs\${{ env.app_x64_self_name }} -DestinationPath ${{ env.app_x64_self_name }}.zip

      # Create release page: https://github.com/ncipollo/release-action
      - name: Create release
        id: create_release
        uses: ncipollo/release-action@v1
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
          tag: v${{ env.version }}
          name: Ver ${{ env.version }}
          body: |
            - Change design
            - Bug fix
          draft: true
          prerelease: false
          artifacts: "${{ env.app_x64_framework_name }}.zip, ${{ env.app_x64_self_name }}.zip"

      # Remove artifacts to save space: https://github.com/c-hive/gha-remove-artifacts
      - name: Remove old artifacts
        uses: c-hive/gha-remove-artifacts@v1
        with:
          age: '1 weeks'
          skip-recent: 2
```

上の YAML でデプロイ方式の フレームワーク依存 と 自己完結（実行PCでランタイム不要）の2つの exe をビルドしています。



ソリューション構成に応じて、先頭の `env:` を適宜書き換えて下さい。

```yaml
env:
  App_Name: WpfGitHubActionsDemo                                           # アプリケーション名
  Solution_Path: WpfGitHubActionsDemo.sln                                  # ソリューションのファイルPATH
  App_Project_Path: src/WpfGitHubActionsDemo/WpfGitHubActionsDemo.csproj   # アプリプロジェクトのファイルPATH
```

## Actions の動作確認

リリース動作を確認するため Git で tag を作成します。 作成した YAML はタグ先頭の `v` をトリガに動作しますので、必ず含めましょう。

git コマンドの実行例

``` powershell
git tag v1.2.3
git push origin v1.2.3
```



## おわりに

Gitタグの作成をトリガに 自動で GitHub の Releases ページを作成して、アプリ.exe の zip を登録する GitHub Actions を作成しました。



## 参考ページ

[.NETのWPFアプリケーションをGithub Actionsでビルドする - Zenn](https://zenn.dev/nuits_jp/articles/2022-07-04-net-wpf-build-with-actions)

[自作のWPFアプリを後から自動テスト・DI・CI/CD対応にしてみる。その3 #C# - Qiita](https://qiita.com/soi/items/e5f01c66c0a303a74c30)

[Publishing/Deploying WPF Applications (feat. GitHub Actions) - EASY WPF (.NET Core) - YouTube](https://www.youtube.com/watch?v=VIlDni8-iWM&ab_channel=SingletonSean)

[GitHub Actions ドキュメント - GitHub Docs](https://docs.github.com/ja/actions)

[GitHub Marketplace · Actions to improve your workflow · GitHub](https://github.com/marketplace?type=actions)

## 環境

Visual Studio 2022 17.8.3
.NET 8
