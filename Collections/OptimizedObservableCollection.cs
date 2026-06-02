using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SaemDesk.Collections
{
    /// <summary>
    /// 대량 업데이트 최적화된 ObservableCollection
    /// UI 스레드 블로킹 및 이벤트 오버헤드 최소화
    /// </summary>
    public class OptimizedObservableCollection<T> : ObservableCollection<T>
    {
        // 이벤트 인자는 매번 동일하므로 재사용하여 할당 제거
        private static readonly PropertyChangedEventArgs CountChangedArgs = new(nameof(Count));
        private static readonly PropertyChangedEventArgs IndexerChangedArgs = new("Item[]");
        private static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public OptimizedObservableCollection() : base()
        {
        }

        /// <summary>
        /// 초기 컬렉션으로 생성
        /// </summary>
        public OptimizedObservableCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <summary>
        /// 대량 추가 (이벤트 일괄 처리)
        /// 백킹 리스트에 직접 추가 후 알림을 1회만 발생 — 항목별 Count/Item[] 알림 폭주 제거.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                Items.Add(item);
            }
            RaiseReset();
        }

        /// <summary>
        /// 대량 제거 (이벤트 일괄 처리)
        /// </summary>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                Items.Remove(item);
            }
            RaiseReset();
        }

        /// <summary>
        /// 전체 교체 (Clear + AddRange 최적화)
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
            RaiseReset();
        }

        /// <summary>
        /// 백킹 리스트를 직접 수정한 뒤 호출 — Count/Item[]/Reset 을 1회씩만 통지.
        /// </summary>
        private void RaiseReset()
        {
            OnPropertyChanged(CountChangedArgs);
            OnPropertyChanged(IndexerChangedArgs);
            OnCollectionChanged(ResetArgs);
        }
    }
}
