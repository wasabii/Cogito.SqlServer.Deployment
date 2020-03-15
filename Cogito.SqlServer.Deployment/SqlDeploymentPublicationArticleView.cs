﻿using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleView : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentAction> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleViewAction(context.InstanceName, databaseName, publicationName, Name.Expand(context));
        }

    }

}
