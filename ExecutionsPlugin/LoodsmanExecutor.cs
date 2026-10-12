using System;
using System.Collections.Generic;
using System.Data;
using Ascon.Plm.Loodsman.PluginSDK;

namespace ExecutionsPlugin
{
    /// <summary>
    /// Выполняет план через клиентский API плагина (под сессией пользователя):
    /// CheckOut базы -> ConnectToCheckOut -> NewObject2 -> UpAttrValueById -> NewLink -> CheckIn2.
    /// При любой ошибке рабочий проект отменяется, в базе ничего не остаётся.
    /// </summary>
    internal sealed class LoodsmanExecutor
    {
        private sealed class Resolved
        {
            public long Id;
            public string Version = string.Empty;
        }

        private readonly INetPluginCall _call;
        private readonly PluginConfig _config;
        private readonly Action<string> _log;

        public LoodsmanExecutor(INetPluginCall call, PluginConfig config, Action<string> log)
        {
            _call = call;
            _config = config;
            _log = log;
        }

        public void Run(BaseObject baseObject, ExecutionPlan plan)
        {
            var dbName = _call.PluginCall.DBName;
            string checkOutName = null;
            var connected = false;
            var checkedIn = false;
            try
            {
                var created = _call.RunMethod("CheckOut", baseObject.Type, baseObject.Product, baseObject.Version, 0);
                checkOutName = FirstText(created);
                if (string.IsNullOrEmpty(checkOutName))
                {
                    throw new InvalidOperationException($"CheckOut не вернул имя рабочего проекта ({Describe(created)}).");
                }

                _log($"CheckOut '{checkOutName}' для {baseObject.Product}.");

                _call.RunMethod("ConnectToCheckOut", checkOutName, dbName);
                connected = true;

                var resolved = new Dictionary<string, Resolved>
                {
                    [baseObject.Type + "\u0001" + baseObject.Product] =
                        new Resolved { Id = baseObject.IdVersion, Version = baseObject.Version }
                };

                CreateObjects(plan, resolved);
                CreateLinks(plan, resolved);

                _call.RunMethod("CheckIn2", checkOutName, dbName);
                connected = false;
                checkedIn = true;
                _log("CheckIn2 выполнен.");
            }
            catch (Exception)
            {
                if (connected)
                {
                    TryRun("DisconnectCheckOut", checkOutName, dbName);
                }

                if (checkOutName != null && !checkedIn)
                {
                    TryRun("CancelCheckOut2", checkOutName, dbName);
                    _log("Рабочий проект отменён, изменения не сохранены.");
                }

                throw;
            }
        }

        private void CreateObjects(ExecutionPlan plan, Dictionary<string, Resolved> resolved)
        {
            if (plan.Objects.Count == 0)
            {
                return;
            }

            // Строка: [TypeName, Product, State, IsProject]. Уже существующий объект NewObject2 возвращает с _ISNEW = 0.
            var input = new object[plan.Objects.Count, 4];
            for (var i = 0; i < plan.Objects.Count; i++)
            {
                input[i, 0] = plan.Objects[i].Type;
                input[i, 1] = plan.Objects[i].Product;
                input[i, 2] = _config.NewObjectState;
                input[i, 3] = 0;
            }

            var table = _call.GetDataTable("NewObject2", input);
            if (table == null || table.Rows.Count != plan.Objects.Count)
            {
                throw new InvalidOperationException(
                    $"NewObject2 вернул {(table == null ? "null" : table.Rows.Count.ToString())} строк, ожидалось {plan.Objects.Count}.");
            }

            foreach (DataRow row in table.Rows)
            {
                var index = Convert.ToInt32(row["_INDEX"]);
                var spec = plan.Objects[index];
                var errCode = Convert.ToInt32(row["_ERRCODE"]);
                if (errCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Создание {spec.Type} «{spec.Product}»: [{errCode}] {row["_ERRMSG"]}");
                }

                var id = Convert.ToInt64(row["_NEW_ID"]);
                var isNew = Convert.ToInt32(row["_ISNEW"]) == 1;
                resolved[spec.Key] = new Resolved { Id = id, Version = Convert.ToString(row["_NEW_VERSION"]) };

                if (isNew && !string.IsNullOrWhiteSpace(spec.Name))
                {
                    _call.RunMethod("UpAttrValueById", (int)id, _config.NameAttribute, spec.Name, string.Empty, false);
                }

                _log($"{(isNew ? "Создан" : "Найден")} {spec.Type} «{spec.Product}» (id {id}).");
            }
        }

        private void CreateLinks(ExecutionPlan plan, Dictionary<string, Resolved> resolved)
        {
            foreach (var link in plan.Links)
            {
                var parent = resolved[link.Parent.Key];
                var child = resolved[link.Child.Key];

                // Количество одним значением: min = max.
                _call.RunMethod(
                    "NewLink",
                    (int)parent.Id, link.Parent.Type, link.Parent.Product, parent.Version,
                    (int)child.Id, link.Child.Type, link.Child.Product, child.Version,
                    link.Quantity, link.Quantity,
                    _config.UnitId,
                    link.LinkType);

                _log($"Связь «{link.LinkType}»: {link.Parent.Product} -> {link.Child.Product}, количество {link.Quantity}.");
            }
        }

        private void TryRun(string method, params object[] args)
        {
            try
            {
                _call.RunMethod(method, args);
            }
            catch (Exception)
            {
                // Уборка после сбоя: исходная ошибка важнее.
            }
        }

        private static string FirstText(object value)
        {
            if (value is DataTable table)
            {
                return table.Rows.Count > 0 && table.Columns.Count > 0 ? Convert.ToString(table.Rows[0][0]) : null;
            }

            return value == null ? null : Convert.ToString(value);
        }

        private static string Describe(object value) => value == null ? "null" : value.ToString();
    }
}
