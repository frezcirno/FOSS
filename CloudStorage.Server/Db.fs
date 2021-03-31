module CloudStorage.Server.Db

open FSharp.Data.Sql

type Sql = SqlDataProvider<
                DatabaseVendor       = Common.DatabaseProviderTypes.MYSQL,
                ConnectionString     = "Server=localhost;Database=test;User=root;Password=root",
                ConnectionStringName = "DefaultConnectionString",
                IndividualsAmount    = 1000,
                UseOptionTypes       = true
            >
            
let ctx = Sql.GetDataContext()

let tbl_file = ctx.Test.TblFile
