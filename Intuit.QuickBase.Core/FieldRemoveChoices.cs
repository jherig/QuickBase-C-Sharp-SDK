﻿/*
 * Copyright © 2013 Intuit Inc. All rights reserved.
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.opensource.org/licenses/eclipse-1.0.php
 */
using System.Collections.Generic;
using System.Xml.Linq;
using Intuit.QuickBase.Core.Payload;
using Intuit.QuickBase.Core.Uri;

namespace Intuit.QuickBase.Core
{
    public class FieldRemoveChoices : IQObject
    {
        private const string QUICKBASE_ACTION = "API_FieldRemoveChoices";
        private readonly Payload.Payload _fieldAddChoicesPayload;
        private readonly IQUri _uri;

        public FieldRemoveChoices(string ticket, string appToken, string accountDomain, string dbid, int fid, List<string> choices, string userToken = "")
        {
            _fieldAddChoicesPayload = new FieldChoicesPayload(fid, choices);
            //If a user token is provided, use it instead of a ticket
            if (userToken.Length > 0)
            {
                _fieldAddChoicesPayload = new ApplicationUserToken(_fieldAddChoicesPayload, userToken);
            }
            else
            {
                _fieldAddChoicesPayload = new ApplicationTicket(_fieldAddChoicesPayload, ticket);
            }
            _fieldAddChoicesPayload = new ApplicationToken(_fieldAddChoicesPayload, appToken);
            _fieldAddChoicesPayload = new WrapPayload(_fieldAddChoicesPayload);
            _uri = new QUriDbid(accountDomain, dbid);
        }

        public void BuildXmlPayload(ref XElement parent)
        {
            _fieldAddChoicesPayload.GetXmlPayload(ref parent);
        }

        public System.Uri Uri
        {
            get
            {
                return _uri.GetQUri();
            }
        }

        public string Action
        {
            get
            {
                return QUICKBASE_ACTION;
            }
        }

        public XElement Post()
        {
            HttpPost httpXml = new HttpPostXml();
            httpXml.Post(this);
            return httpXml.Response;
        }
    }
}
