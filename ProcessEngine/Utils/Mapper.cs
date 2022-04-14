using ProcessEngine.DTOs;
using ProcessEngine.Engines;
using ProcessEngine.Helpers;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProcessEngine.Utils
{
    internal class Mapper
    {
        private readonly MemoryEngine _memoryEngine;

        public Mapper(MemoryEngine memoryEngine)
        {
            _memoryEngine = memoryEngine;
        }

        public TDestination Map<TSource, TDestination>(TSource sourceStruct)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            TDestination destinationObj = new();

            FieldInfo[] srcFieldsInfos = typeof(TSource).GetFields();
            PropertyInfo[] propertiesInfos = typeof(TDestination).GetProperties();

            (FieldInfo, PropertyInfo)[] fieldsAndCorrespondingProperties = MapperHelper.Intersect(srcFieldsInfos, propertiesInfos);

            Parallel.ForEach(fieldsAndCorrespondingProperties, fieldAndCorrespondingProperty =>
            {
                FieldInfo srcFieldInfo = fieldAndCorrespondingProperty.Item1;
                PropertyInfo dstPropertyInfo = fieldAndCorrespondingProperty.Item2;

                Map(srcFieldInfo, dstPropertyInfo, ref sourceStruct, ref destinationObj);
            });

            FieldInfo[] fieldsWithoutCorrespondingProperties = MapperHelper.SymmetricDifference(srcFieldsInfos, propertiesInfos);

            Parallel.ForEach(fieldsWithoutCorrespondingProperties, fieldWithoutCorrespondingProperty =>
            {
                Type srcFieldType = fieldWithoutCorrespondingProperty.FieldType;
                object deeperStruct = fieldWithoutCorrespondingProperty.GetValue(sourceStruct);

                object deeperDestionationObj = typeof(Mapper)
                    .GetMethod("Map")
                    .MakeGenericMethod(new Type[] { srcFieldType, typeof(TDestination) })
                    .Invoke(this, new object[] { deeperStruct });

                destinationObj = MapperHelper.Combine((TDestination)deeperDestionationObj, destinationObj);
            });

            return destinationObj;
        }

        public TDestination[] MapList<TSource, TDestination>(TSource sourceStruct)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (!MapperHelper.IsListStruct<TSource>())
            {
                throw new Exception($"{nameof(sourceStruct)} is not a list struct");
            }
            FieldInfo srcItemsFieldInfo = typeof(TSource).GetField("Items");
            FieldInfo srcCountFieldInfo = typeof(TSource).GetField("Count");

            Type srcItemsType = srcItemsFieldInfo.FieldType.GetElementType();

            IntPtr itemsPtr;
            unsafe { itemsPtr = (IntPtr)Pointer.Unbox(srcItemsFieldInfo.GetValue(sourceStruct)); }
            if (itemsPtr.Equals(IntPtr.Zero))
            {
                return new TDestination[0];
            }
            int itemsCount = (int)srcCountFieldInfo.GetValue(sourceStruct);
            IntPtr[] sourcesPtrs = _memoryEngine.ReadArray<IntPtr>(itemsPtr, itemsCount);

            ConcurrentBag<TDestination> destinations = new();
            Parallel.ForEach(sourcesPtrs, sourcePtr =>
            {
                object destionation = typeof(Mapper)
                    .GetMethod("ReadAndMap", new[] { typeof(IntPtr) })
                    .MakeGenericMethod(new Type[] { srcItemsType, typeof(TDestination) })
                    .Invoke(this, new object[] { sourcePtr });
                destinations.Add((TDestination)destionation);
            });

            return destinations.ToArray();
        }

        public TDestination ReadAndMap<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            TDestination destinationObj = Map<TSource, TDestination>(
                _memoryEngine.Read<TSource>(pointer));
            destinationObj.NativePtr = pointer;

            return destinationObj;
        }

        public TDestination[] ReadAndMapList<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (!MapperHelper.IsListStruct<TSource>())
            {
                throw new Exception($"{nameof(TSource)} is not a list struct");
            }
            else if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return MapList<TSource, TDestination>(
                _memoryEngine.Read<TSource>(pointer));
        }

        private void Map<TSource, TDestination>(
            FieldInfo srcFieldInfo,
            PropertyInfo dstPropertyInfo,
            ref TSource sourceStruct,
            ref TDestination destinationObj)
        {
            if (srcFieldInfo is null)
            {
                throw new ArgumentNullException(nameof(srcFieldInfo));
            }

            if (dstPropertyInfo is null)
            {
                throw new ArgumentNullException(nameof(dstPropertyInfo));
            }

            object valueToSetProperty = null;

            Type srcfieldType = srcFieldInfo.FieldType;
            Type dstpropertyType = dstPropertyInfo.PropertyType;

            if (dstpropertyType.IsGenericType && dstpropertyType.GenericTypeArguments.First().Equals(srcFieldInfo.FieldType)
                || srcfieldType.Equals(dstpropertyType))
            {
                valueToSetProperty = srcFieldInfo.GetValue(sourceStruct);
            }
            else if (dstpropertyType.Equals(typeof(bool)))
            {
                valueToSetProperty = (byte)srcFieldInfo.GetValue(sourceStruct) == 1;
            }
            else if (srcfieldType.IsPointer)
            {
                IntPtr nestedStructPtr;
                unsafe { nestedStructPtr = (IntPtr)Pointer.Unbox(srcFieldInfo.GetValue(sourceStruct)); }
                if (nestedStructPtr.Equals(IntPtr.Zero))
                {
                    return;
                }
                else if (dstpropertyType.Equals(typeof(string)))
                {
                    valueToSetProperty = _memoryEngine.ReadString(nestedStructPtr);
                }
                else
                {
                    string methodToCall;

                    srcfieldType = srcfieldType.GetElementType();
                    if (dstpropertyType.IsArray && MapperHelper.IsListStruct(srcfieldType))
                    {
                        dstpropertyType = dstpropertyType.GetElementType();
                        methodToCall = "ReadAndMapList";
                    }
                    else
                    {
                        methodToCall = "ReadAndMap";
                    }

                    valueToSetProperty = typeof(Mapper)
                        .GetMethod(methodToCall)
                        .MakeGenericMethod(new Type[] { srcfieldType, dstpropertyType })
                        .Invoke(this, new object[] { nestedStructPtr });
                }
            }

            dstPropertyInfo.SetValue(destinationObj, valueToSetProperty);
        }
    }
}