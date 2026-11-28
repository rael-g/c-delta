[Import("sqlite3.h", Only = ["sqlite3_open", "sqlite3_close", "sqlite3_exec", "sqlite3_prepare_v2", "sqlite3_step", "sqlite3_column_int", "sqlite3_finalize", "SQLITE_OK", "SQLITE_ROW", "SQLITE_DONE"])]
static unsafe partial class Sq { }

static unsafe class Program
{
    static int Run(Sq.sqlite3* database, string sql)
    {
        fixed (byte* text = sql.Bytes)
        {
            return Sq.sqlite3_exec(database, text, null, null, null);
        }
    }

    static int Main()
    {
        Sq.sqlite3* database = null;
        fixed (byte* memory = ":memory:".Bytes)
        {
            if (Sq.sqlite3_open(memory, &database) != Sq.SQLITE_OK)
            {
                return 1;
            }
        }

        if (Run(database, "CREATE TABLE numbers (value INTEGER)") != Sq.SQLITE_OK)
        {
            return 2;
        }

        if (Run(database, "INSERT INTO numbers VALUES (10), (20), (12)") != Sq.SQLITE_OK)
        {
            return 3;
        }

        Sq.sqlite3_stmt* statement = null;
        int total = 0;
        fixed (byte* query = "SELECT value FROM numbers".Bytes)
        {
            if (Sq.sqlite3_prepare_v2(database, query, -1, &statement, null) != Sq.SQLITE_OK)
            {
                return 4;
            }
        }

        while (Sq.sqlite3_step(statement) == Sq.SQLITE_ROW)
        {
            total += Sq.sqlite3_column_int(statement, 0);
        }

        Sq.sqlite3_finalize(statement);
        Sq.sqlite3_close(database);
        return total;
    }
}
