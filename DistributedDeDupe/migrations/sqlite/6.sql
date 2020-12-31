create table directories
(
	id integer not null
		constraint folders_pk
			primary key autoincrement,
	dirname text,
	fullpath text
);

INSERT INTO directories(id, dirname, fullpath) VALUES (1, '/', null);