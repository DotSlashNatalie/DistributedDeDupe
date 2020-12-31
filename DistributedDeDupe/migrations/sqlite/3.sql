create table entities
(
	id integer not null
		constraint files_pk
			primary key autoincrement,
	fname text,
	dir integer,
	size integer,
	cdate integer,
	mdate integer,
	accessdate integer,
	isdir bool,
	filehash text
);

create index files_dir_index
	on entities (dir);

create index files_fname_dir_index
	on entities (fname, dir);

