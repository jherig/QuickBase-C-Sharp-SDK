﻿/*
 * Copyright © 2010 Intuit Inc. All rights reserved.
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.opensource.org/licenses/eclipse-1.0.php
 */
using Intuit.QuickBase.Core;

namespace Intuit.QuickBase.Client
{
    internal class QColumnFactory : QColumnFactoryBase
    {
        private static QColumnFactoryBase _instance;

        private QColumnFactory() { }

        internal static QColumnFactoryBase GetInstance()
        {
            if(_instance == null)
            {
                _instance = new QColumnFactory();
            }
            return _instance;
        }

        internal override IQColumn CreateInstance(int columnId, string columnName, FieldType columnType, string columnRole, bool columnVirtual, bool columnLookup, bool columnSummary, bool ishidden, bool allowHTML, bool canAddChoices)
        {
            return new QColumn(columnId, columnName, columnType, columnRole, columnVirtual, columnLookup, columnSummary, ishidden, allowHTML, canAddChoices);
        }
    }
}
