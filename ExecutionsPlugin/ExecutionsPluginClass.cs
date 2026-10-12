using System;
using System.Data;
using System.Windows;
using Ascon.Plm.Loodsman.PluginSDK;

namespace ExecutionsPlugin
{
    [LoodsmanPlugin]
    public class ExecutionsPluginClass : ILoodsmanNetPlugin
    {
        public void BindMenu(IMenuDefinition menu)
        {
            menu.AddMenuItem("Исполнения#Создать исполнения", OpenForm, IsAssemblySelected);
        }

        private static bool IsAssemblySelected(INetPluginCall call)
        {
            try
            {
                if (call == null || call.PluginCall.IdVersion <= 0)
                {
                    return false;
                }

                return string.Equals(call.PluginCall.stType, PluginConfig.Load().AssemblyType, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void OpenForm(INetPluginCall call)
        {
            try
            {
                var config = PluginConfig.Load();
                var baseObject = new BaseObject
                {
                    IdVersion = call.PluginCall.IdVersion,
                    Type = call.PluginCall.stType,
                    Product = call.PluginCall.stProduct,
                    Version = call.PluginCall.stVersion,
                    Name = ReadName(call, config, call.PluginCall.IdVersion)
                };

                new ExecutionsForm(call, config, baseObject).ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Исполнения: ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ReadName(INetPluginCall call, PluginConfig config, long idVersion)
        {
            try
            {
                // GetInfoAboutVersion, режим 2: атрибуты объекта ([_NAME], [_VALUE]).
                var table = call.GetDataTable("GetInfoAboutVersion", string.Empty, string.Empty, string.Empty, (int)idVersion, 2);
                if (table == null)
                {
                    return string.Empty;
                }

                foreach (DataRow row in table.Rows)
                {
                    if (string.Equals(Convert.ToString(row["_NAME"]), config.NameAttribute, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToString(row["_VALUE"]);
                    }
                }
            }
            catch (Exception)
            {
                // Наименование необязательно: без него исполнения создадутся без наименования.
            }

            return string.Empty;
        }

        public void OnCloseDb()
        {
        }

        public void OnConnectToDb(INetPluginCall call)
        {
        }

        public void PluginLoad()
        {
        }

        public void PluginUnload()
        {
        }
    }
}
