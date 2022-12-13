# 「Unityからブラウザまで！　ロジックの切り出しに WASI を使ってみる」 サンプルリポジトリ

## 内容
- compiler
    - `wasm` を `wasmu` に変換するコンパイラの実装です
- fib
    - ベンチマーク用に `fib(42)` を求める実装です
- loader
    - Unityで `wasm` と `wasmu` を実行するためにWasmerを組み込むためのネイティブプラグインの実装です
- mandelbrot
    - マンデルブロ集合のPBM画像を生成する実装です
- unity/mandelbrot
    - マンデルブロ集合の画像を生成して表示するUnityのサンプルプロジェクトです

## 動作を確認した環境
- M1 MacBook Pro（2021）
- Unity 2021.3.15f
- xLua v2.1.16
- LuaJIT 2.1.0-beta3
- rustc 1.65.0
- Wasmer 3.0.2

## ライセンス
- 本リポジトリはデュアルライセンスを採用しています。ライセンスが明示されていないものについては、以下からいずれか好きなライセンスを選択してコードを利用してください
    - CC0 1.0 Universal
        - 事実上のパブリックドメインです（指定がない場合、こちらが採用されるものとします）
    - Creative Commons Attribution 4.0 International Public License
        - 記事自体のライセンスです
- ただし、別途LICENSEファイルが置かれている部分については、そちらに従ってください
    - マンデルブロ集合のサンプルコードは [The Computer Language 22.05 Benchmarks Game](https://benchmarksgame-team.pages.debian.net/benchmarksgame/index.html) から借用しています（[修正BSDライセンス](https://benchmarksgame-team.pages.debian.net/benchmarksgame/license.html)）
