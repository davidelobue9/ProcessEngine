using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProcessEngine.Helpers
{
    internal class MapperHelper
    {
        public static T Combine<T>(T obj0, T obj1)
        {
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                var defaultValue = propertyType.GetTypeInfo().IsValueType ? Activator.CreateInstance(propertyType) : null;

                var propValueTmp = property.GetValue(obj0);

                if (propValueTmp is null || propValueTmp.Equals(defaultValue))
                {
                    property.SetValue(obj0, property.GetValue(obj1));
                }
            }

            return obj0;
        }

        public static (FieldInfo, PropertyInfo)[] Intersect(FieldInfo[] structFieldsInfo, PropertyInfo[] objPropertiesInfo)
        {
            List<(FieldInfo, PropertyInfo)> intersection = new();

            for (int i = 0; i < structFieldsInfo.Length; i++)
            {
                for (int j = 0; j < objPropertiesInfo.Length; j++)
                {
                    if (structFieldsInfo[i].Name.Equals(objPropertiesInfo[j].Name))
                    {
                        intersection.Add((structFieldsInfo[i], objPropertiesInfo[j]));
                    }
                }
            }

            return intersection.ToArray();
        }

        public static bool IsArrayObject<TObject>()
        {
            return typeof(TObject).IsArray;
        }

        public static bool IsListStruct<TStruct>()
            where TStruct : struct
        {
            return IsListStruct(typeof(TStruct));
        }

        public static bool IsListStruct(Type structType)
        {
            return structType.GetField("Items") is not null && structType.GetField("Count") is not null;
        }

        public static FieldInfo[] SymmetricDifference(FieldInfo[] structFieldsInfo, PropertyInfo[] objPropertiesInfo)
        {
            List<FieldInfo> symmetricDifference = new();

            for (int i = 0; i < structFieldsInfo.Length; i++)
            {
                bool canAddToList = true;
                for (int j = 0; j < objPropertiesInfo.Length; j++)
                {
                    if (structFieldsInfo[i].Name == objPropertiesInfo[j].Name)
                    {
                        canAddToList = false;
                        break;
                    }
                }
                if (canAddToList)
                {
                    symmetricDifference.Add(structFieldsInfo[i]);
                }
            }

            return symmetricDifference.ToArray();
        }
    }
}