// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace System.Text.Yaml.Nodes
{
    public sealed partial class YamlArray : YamlNode, IList<YamlNode?>
    {
        /// <summary>
        ///   Gets the number of elements contained in the <see cref="YamlArray"/>.
        /// </summary>
        public int Count => List.Count;

        /// <summary>
        ///   Adds a <see cref="YamlNode"/> to the end of the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="item">
        ///   The <see cref="YamlNode"/> to be added to the end of the <see cref="YamlArray"/>.
        /// </param>
        public void Add(YamlNode? item)
        {
            item?.AssignParent(this);

            List.Add(item);
            ItemCommentList?.Add(null);
        }

        /// <summary>
        ///   Removes all elements from the <see cref="YamlArray"/>.
        /// </summary>
        public void Clear()
        {
            List<YamlNode?>? list = _list;

            if (list is null)
            {
                if (_yamlElement is { } element)
                {
                    CaptureCollectionStyle(element);
                    CaptureCollectionComments(element);
                }

                _yamlElement = null;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    DetachParent(list[i]);
                }

                list.Clear();
                ItemCommentList = null;
            }
        }

        /// <summary>
        ///   Determines whether an element is in the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="YamlArray"/>.</param>
        /// <returns>
        ///   <see langword="true"/> if <paramref name="item"/> is found in the <see cref="YamlArray"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Contains(YamlNode? item) => List.Contains(item);

        /// <summary>
        ///   The object to locate in the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="item">The <see cref="YamlNode"/> to locate in the <see cref="YamlArray"/>.</param>
        /// <returns>
        ///  The index of item if found in the list; otherwise, -1.
        /// </returns>
        public int IndexOf(YamlNode? item) => List.IndexOf(item);

        /// <summary>
        ///   Inserts an element into the <see cref="YamlArray"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="YamlNode"/> to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0 or <paramref name="index"/> is greater than <see cref="Count"/>.
        /// </exception>
        public void Insert(int index, YamlNode? item)
        {
            item?.AssignParent(this);
            List.Insert(index, item);
            ItemCommentList?.Insert(index, null);
        }

        /// <summary>
        ///   Removes the first occurrence of a specific <see cref="YamlNode"/> from the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="item">
        ///   The <see cref="YamlNode"/> to remove from the <see cref="YamlArray"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if <paramref name="item"/> is successfully removed; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Remove(YamlNode? item)
        {
            int index = List.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        /// <summary>
        ///   Removes the element at the specified index of the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0 or <paramref name="index"/> is greater than <see cref="Count"/>.
        /// </exception>
        public void RemoveAt(int index)
        {
            YamlNode? item = List[index];
            List.RemoveAt(index);
            ItemCommentList?.RemoveAt(index);
            DetachParent(item);
        }

        /// <summary>
        ///   Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from the <see cref="YamlArray"/>.</returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="match"/> is <see langword="null"/>.
        /// </exception>
        public int RemoveAll(Func<YamlNode?, bool> match)
        {
            ArgumentNullException.ThrowIfNull(match);

            List<YamlNode?> items = List; // materialized first: that is when the items' comments are taken
            if (ItemCommentList is { } itemComments)
            {
                // The items' comments go with them: both lists are compacted in one pass, in the items' order.
                int kept = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    YamlNode? node = items[i];
                    if (match(node))
                    {
                        DetachParent(node);
                        continue;
                    }

                    items[kept] = node;
                    itemComments[kept] = i < itemComments.Count ? itemComments[i] : null;
                    kept++;
                }

                int removed = items.Count - kept;
                items.RemoveRange(kept, removed);
                if (itemComments.Count > kept)
                {
                    itemComments.RemoveRange(kept, itemComments.Count - kept);
                }

                return removed;
            }

            return items.RemoveAll(node =>
            {
                if (match(node))
                {
                    DetachParent(node);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }

        /// <summary>
        ///   Removes a range of elements from the <see cref="YamlArray"/>.
        /// </summary>
        /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
        /// <param name="count">The number of elements to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> or <paramref name="count"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="index"/> and <paramref name="count"/> do not denote a valid range of elements in the <see cref="YamlArray"/>.
        /// </exception>
        public void RemoveRange(int index, int count)
        {
            if (index < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(nameof(index));
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(nameof(count));
            }

            List<YamlNode?> list = List;

            if (list.Count - index < count)
            {
                ThrowHelper.ThrowArgumentException_InvalidOffLen();
            }

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    DetachParent(list[index + i]);
                    // There's no need to assign nulls because List<>.RemoveRange calls
                    // Array.Clear on the removed partition.
                }

                list.RemoveRange(index, count);
                ItemCommentList?.RemoveRange(index, count);
            }
        }

        #region Explicit interface implementation

        /// <summary>
        ///   Copies the entire <see cref="Array"/> to a compatible one-dimensional array,
        ///   starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">
        ///   The one-dimensional <see cref="Array"/> that is the destination of the elements copied
        ///   from <see cref="YamlArray"/>. The Array must have zero-based indexing.</param>
        /// <param name="index">
        ///   The zero-based index in <paramref name="array"/> at which copying begins.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The number of elements in the source ICollection is greater than the available space from <paramref name="index"/>
        ///   to the end of the destination <paramref name="array"/>.
        /// </exception>
        void ICollection<YamlNode?>.CopyTo(YamlNode?[] array, int index) => List.CopyTo(array, index);

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="YamlArray"/>.
        /// </summary>
        /// <returns>A <see cref="IEnumerator{YamlNode}"/> for the <see cref="YamlNode"/>.</returns>
        public IEnumerator<YamlNode?> GetEnumerator() => List.GetEnumerator();

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="YamlArray"/>.
        /// </summary>
        /// <returns>
        ///   A <see cref="IEnumerator"/> for the <see cref="YamlArray"/>.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)List).GetEnumerator();

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<YamlNode?>.IsReadOnly => false;

        #endregion

        private static void DetachParent(YamlNode? item)
        {
            item?.Parent = null;
        }
    }
}
