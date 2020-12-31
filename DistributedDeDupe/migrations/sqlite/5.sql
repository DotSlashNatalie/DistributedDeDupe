create table blocks
(
	id integer not null
		constraint blocks_pk
			primary key autoincrement,
	hash1 text,
	hash2 text,
	size integer,
	name text,
	location text
);

