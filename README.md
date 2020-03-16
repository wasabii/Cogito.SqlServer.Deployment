# Cogito.SqlServer.Deployment
The `Cogito.SqlServer.Deployment` package provides a library to enable deployment and configuration, in batch, of SQL Server instances. Thought DACPACs provide a convient way to package the schema and objects associated with a single database; in a continuous deployment situation often other higher-level SQL Server configuration would be useful to deploy. For instance, Linked Servers. Or multiple DACPACs. Or complex replication topologies.

## Configuration
`Cogito.SqlServer.Deployment` works by processing a SQL deployment manifest file. This file is an XML file which defines a number of named `Target` elements. Each `Target` element can contain one or more `Instance` elements. And within each `Instance` element configuration can be specified.

`Target` elements can also define `DependsOn` elements to specify `Target`s that must be run successfully first. This allows complex dependency hierarchies to be built, and deployment of a single required `Target` to commense without encuring the cost of deploying more than is strictly necessary for the task. This facilitates unit testing across multiple SQL instances or SQL databases. Unit tests need only initiate the deployment for the target that they specifically require.

The following example demonstrates the configuration of two LocalDB instances, each one containing a single database deployed from a DACPAC.

```
<Deployment xmlns="https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020">
    <Parameter Name="InstanceNameA" DefaultValue="(localdb)\InstanceA" />
    <Parameter Name="InstanceNameB" DefaultValue="(localdb)\InstanceB" />
    <Parameter Name="PathToDacPacA" />
    <Parameter Name="PathToDacPacB" />
    <Target Name="TargetA">
        <Instance Name="[InstanceNameA]">
            <Database Name="DatabaseA" Owner="sa">
                <Package Source="[PathToDacPacA]" />
            </Database>
        </Instance>
    </Target>
    <Target Name="TargetB">
        <Instance Name="[InstanceNameB]">
            <Database Name="DatabaseB" Owner="sa">
                <Package Source="[PathToDacPacB]" />
            </Database>
        </Instance>
    </Target>
</Deployment>
```

## Tool
A .NET Core Global Tool is available as `Cogito.SqlServer.Deployment.Tool`. This tool supports a `deploy` command, accepting the manifest path as an argument; along with additional `/p` arguments to specify parameters.

```
dotnet sqldeploy Environment.xml -p:Foo=Bar
```

## Unit Testing
In addition to deployment of environmental instances, unit testing against complex SQL server instances can be made more convienent by being able to deploy a complex SQL server topology to LocalDB instances.