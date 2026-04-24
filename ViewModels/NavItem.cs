using System;

namespace SaemDesk.ViewModels;

/// <summary>
/// 좌측 네비게이션 패널의 메뉴 항목.
/// Factory는 AOT-safe 람다로 ViewModel을 생성합니다.
/// </summary>
public record NavItem(
    string Title,
    string Icon,        // Unicode 문자 또는 향후 PathIcon 키
    Func<ViewModelBase> Factory);
