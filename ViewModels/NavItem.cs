using System;
using System.Collections.Generic;

namespace SaemDesk.ViewModels;

/// <summary>
/// 좌측 네비게이션 패널의 메뉴 항목.
/// - 리프 항목: <see cref="Factory"/> 가 ViewModel 을 생성. <see cref="Children"/> = null.
/// - 그룹 항목: <see cref="Factory"/> = null. <see cref="Children"/> 에 자식 리프/그룹.
/// Factory 는 AOT-safe 람다로 ViewModel 을 생성합니다.
/// </summary>
public class NavItem
{
    public string Title { get; }
    public string Icon  { get; }
    public Func<ViewModelBase>? Factory { get; }
    public IReadOnlyList<NavItem>? Children { get; }

    /// <summary>리프 항목 — 클릭 시 ViewModel 생성·페이지 전환.</summary>
    public NavItem(string title, string icon, Func<ViewModelBase> factory)
    {
        Title    = title;
        Icon     = icon;
        Factory  = factory;
        Children = null;
    }

    /// <summary>그룹 항목 — 자식을 펼침/접힘. 클릭 시 페이지 전환 없음.</summary>
    public NavItem(string title, string icon, IReadOnlyList<NavItem> children)
    {
        Title    = title;
        Icon     = icon;
        Factory  = null;
        Children = children;
    }

    /// <summary>그룹 노드 여부.</summary>
    public bool IsGroup => Factory is null;
}
