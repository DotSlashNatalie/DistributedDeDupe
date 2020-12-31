create table fileblocks
(
	id integer not null
		constraint fileblocks_pk
			primary key autoincrement,
	file_id integer
		constraint fileblocks_files_id_fk
			references files,
	block_id integer,
	block_order integer
);

create index fileblocks_file_id_block_order_index
	on fileblocks (file_id, block_order);

