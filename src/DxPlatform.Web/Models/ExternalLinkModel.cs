namespace DxPlatform.Web.Models;

/// <summary>詳細画面で外部リンクを表示するための部分ビューのモデル（Urlがnullでも部分ビューが親のモデルに置き換わらないよう、オブジェクトで渡す）。</summary>
public sealed record ExternalLinkModel(string? Url);
