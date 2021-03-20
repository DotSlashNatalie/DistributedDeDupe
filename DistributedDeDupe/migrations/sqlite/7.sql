create table filestorage
(
	id integer
		constraint filestorage_pk
			primary key autoincrement,
	name text,
	start integer,
	end integer,
	size integer
);

create index filestorage_name_index
	on filestorage (name);

